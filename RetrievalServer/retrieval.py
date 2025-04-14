import faiss
from langchain_community.docstore.in_memory import InMemoryDocstore
from langchain_community.vectorstores import FAISS
from transformers import AutoTokenizer
from enum import Enum
from langchain_huggingface.embeddings import HuggingFaceEmbeddings
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_experimental.text_splitter import SemanticChunker
from langchain_community.retrievers import BM25Retriever, TFIDFRetriever
from langchain.retrievers import EnsembleRetriever
from database import DBInstance
from docloader import Loader
from langchain_core.runnables import ConfigurableField
from langchain_core.documents import Document
import shutil
from os.path import isdir
import os
import torch

class ConfigText(Enum):
    VS_R_K = "int from 1 to whatever, k retrieved documents from vectorstore retriever, default: 4, current: "
    SECOND_R_K = "int from 1 to whatever, k retrieved documents from second retriever, default: 4, current: "
    SSPLIT_BREAKPOINT_THR = "float from 0.1 to 99.9, precentages(%) of semantic difference of text pieces, default: 80.0, current: "
    RENSEMBLE_WEIGHTS = "float from 0.0 to 1.0, changes weight of first retriever in ensemble, weight of second calculated via 1-<value>, default: 0.5, current: "
    SECOND_R_TYPE = "bm25 or tf-idf, changes type of second retriever, default: bm25, current: "
    SECOND_SPLIT_TYPE = "semantic or recursive, changes type of text splitter for second retriever, default: semantic, current: "
    RECURSIVE_CHUNK_SIZE = "int from 100 to whatever, changes size of chunks(in tokens) split by recursive splitter, default: 200, current: "

class Model:
    _load_path = ".\\docs\\to_load"
    _retriever_config_fields = {"vs_r": [("vs_r_k", "k")], "bm25": ["second_r_k"], "tf-idf": ["second_r_k"]}
    _toSave = []
    _lastId = None
    def __init__(self, load_second_r: bool = True):
        # Initialize dependencies
        os.environ["PYTORCH_CUDA_ALLOC_CONF"] = "max_split_size_mb:512"
        DBInstance.instantiate()
        Loader.recursive_load(Model._load_path)
        self.config_fields = {
            "vs_r_k": [10, f"{ConfigText.VS_R_K.value}"],
            "second_r_k": [0, f"{ConfigText.SECOND_R_K.value}"],
            "second_r_type": ["bm25", f"{ConfigText.SECOND_R_TYPE.value}"],
            "second_split_type": ["semantic", f"{ConfigText.SECOND_SPLIT_TYPE.value}"],
            "semantic_breakpoint_thr": [94.5, f"{ConfigText.SSPLIT_BREAKPOINT_THR.value}"],
            "rensemble_weights": [1, f"{ConfigText.RENSEMBLE_WEIGHTS.value}"],
            "recursive_chunk_size": [200, f"{ConfigText.RECURSIVE_CHUNK_SIZE.value}"]
        }
        self._current_second_r_type = self.config_fields["second_r_type"][0]
        self._config_fields_funcs = {
            "second_r_type": self._config_load_second_retriever,
            "second_split_type": self._config_load_second_splitter,
            "semantic_breakpoint_thr": self._config_semantic,
            "recursive_chunk_size": self._config_recursive
        }

        # Initialize chunkers and embedders
        self._embeddings = HuggingFaceEmbeddings(
            model_name="C:\\codes\\epl_embedder\\jina-embeddings-v3", 
            model_kwargs={"device": "cuda:0", "trust_remote_code": True}
        )
        self._tokenizer = AutoTokenizer.from_pretrained(
            "./jina-embeddings-v3/", trust_remote_code=True
        )
        # The splitters to use to create smaller chunks
        self._semantic_chunker = SemanticChunker(
            embeddings=self._embeddings, 
            breakpoint_threshold_amount=self.config_fields["semantic_breakpoint_thr"][0]
        )
        self._recursive_chunker = RecursiveCharacterTextSplitter(
            chunk_size=self.config_fields["recursive_chunk_size"][0],
            chunk_overlap=0,
            length_function=len,
            is_separator_regex=False,
        )
        # Initialize the second retriever
        self._load_second_r = load_second_r
        if not load_second_r:
            self.config_fields["second_r_k"][0] = 0
        self._config_load_second_splitter()

        # The vectorstore retriever + ensemble retriever
        self._initialize_faiss_and_ensemble()

    def on_close(self):
        self._vectorstore.save_local(".\\faiss_index")
        with open("./processed.txt", "a") as f:
            for d in Model._toSave:
                f.write(f"{d}\n")
        with open("./lastid.txt", mode="w") as f:
                f.write(f"{Model._lastId}")
        DBInstance.close()

    def _initialize_faiss_and_ensemble(self):
        if isdir(".\\faiss_index"):
            self._vectorstore = FAISS.load_local(".\\faiss_index", self._embeddings, allow_dangerous_deserialization=True)
        else:
            index = faiss.IndexFlatL2(len(self._embeddings.embed_query("hello world")))

            self._vectorstore = FAISS(
            embedding_function=self._embeddings,
            index=index,
            docstore=InMemoryDocstore(),
            index_to_docstore_id={},
            )
        self._vectorstore_retriever = self._vectorstore.as_retriever().configurable_fields(
            search_kwargs=ConfigurableField(
                id="vs_r_args",
                name="Retrieved Documents",
                description="Amount of retrieved documents."
            )
        )
        self._ensemble_retriever = EnsembleRetriever(
            retrievers=[self._second_retriever, self._vectorstore_retriever], 
            weights=[self.config_fields["rensemble_weights"][0], 1-self.config_fields["rensemble_weights"][0]]
        )

    def _config_semantic(self):
        self._reset_only_vectorstore()
        if self.config_fields["second_split_type"][0] == "semantic":
            self._config_load_second_splitter()

    def _config_recursive(self):
        if self.config_fields["second_split_type"][0] == "recursive":
            self._config_load_second_splitter()

    def _reset_vectorstore(self):
        self._ensemble_retriever = None
        self._vectorstore_retriever = None
        self._vectorstore = None
        # with open("./lastid.txt", mode="w") as f:
        #         f.write("0")
        # if isdir(".\\faiss_index"):
        #     shutil.rmtree(".\\faiss_index")
        self._initialize_faiss_and_ensemble()

    def _reset_only_vectorstore(self):
        docs = Loader.get_docs_from_table("Loaded")
        self._reset_vectorstore()
        self._update_vectorstore(docs)

    def _config_load_second_splitter(self):
        "Also loads second_retriever"
        match self.config_fields["second_split_type"][0]:
            case "semantic":
                self._second_r_splitter = self._semantic_split
            case "recursive":
                self._second_r_splitter = self._recursive_split
            case _:
                self._second_r_splitter = self._semantic_split
        self._config_load_second_retriever(force_load=True)

    def _semantic_split(self, docs: list[Document], last_id = 1):
        counter = last_id
        sub_docs = []
        ids = []
        for doc in docs:
            _url = doc.metadata["url"]
            _title = doc.metadata["title"]
            _sub_docs = self._semantic_chunker.split_documents([doc])
            for _doc in _sub_docs:
                if _doc.page_content.strip():
                    _doc.metadata["url"] = _url
                    _doc.metadata["id"] = counter
                    _doc.metadata["title"] = _title
                    ids.append(counter)
                    counter+=1
                else:
                    _sub_docs.remove(_doc)
            sub_docs.extend(_sub_docs)
            print(counter)
        return (sub_docs, ids)

    def _recursive_split(self, docs: list[Document]):
        sub_docs = []
        for doc in docs:
            _url = doc.metadata["url"]
            _sub_docs = self._recursive_chunker.split_documents([doc])
            for _doc in _sub_docs:
                _doc.metadata["url"] = _url
            sub_docs.extend(_sub_docs)
        return (sub_docs, None)

    def _config_load_second_retriever(self, force_load: bool = False, **kwargs):
        print("Loaders or smth")
        if self.config_fields["second_r_type"][0] != self._current_second_r_type or force_load:
            match self.config_fields["second_r_type"][0]:
                case "bm25":
                    self._second_retriever = self._load_bm25(**kwargs)
                case "tf-idf":
                    self._second_retriever = self._load_tfidf(**kwargs)
                case _:
                    self._second_retriever = self._load_bm25(**kwargs)
            self._current_second_r_type = self.config_fields["second_r_type"][0]

    def _load_tfidf(self, **kwargs):
        docs = []
        if "docs" in kwargs:
            docs = kwargs["docs"]
        else:
            docs = Loader.get_docs_from_table("Loaded")
        if docs and self._load_second_r:
            docs = self._second_r_splitter(docs)[0]
            return TFIDFRetriever.from_documents(docs, preprocess_func=self._tokenize).configurable_fields(
                k=ConfigurableField(
                    id="second_r_k",
                    name="Retrieved Documents",
                    description="Amount of retrieved documents."
                    )
                )
        return TFIDFRetriever.from_documents([Document("1")]).configurable_fields(
                k=ConfigurableField(
                    id="second_r_k",
                    name="Retrieved Documents",
                    description="Amount of retrieved documents."
                    )
                )
    
    def _load_bm25(self, **kwargs):
        docs = []
        if "docs" in kwargs:
            docs = kwargs["docs"]
        else:
            docs = Loader.get_docs_from_table("Loaded")
        if docs and self._load_second_r:
            docs = self._second_r_splitter(docs)[0]
            return BM25Retriever.from_documents(docs, preprocess_func=self._tokenize).configurable_fields(
                k=ConfigurableField(
                    id="second_r_k",
                    name="Retrieved Documents",
                    description="Amount of retrieved documents."
                    )
                )
        return BM25Retriever.from_documents([Document("1")]).configurable_fields(
                k=ConfigurableField(
                    id="second_r_k",
                    name="Retrieved Documents",
                    description="Amount of retrieved documents."
                    )
                )

    def config(self, fields: dict):
        print("configuring")
        for f in self.config_fields.keys():
            if f in fields:
                self.config_fields[f][0] = fields[f]
                if f in self._config_fields_funcs:
                    self._config_fields_funcs[f]()

    def _tokenize(self, text: str) -> list[str]:
        return self._tokenizer(text, return_tensors="pt").to("cuda:0").tokens()

    def reset_loaded_docs(self):
        # self._reset_vectorstore()
        DBInstance.reset_table("Loaded")
        self.update(load=False, load_second=False)

    def reset_processed_docs(self):
        DBInstance.reset_table("Processed")
        Loader.recursive_load(Model._load_path)

    def reset_db(self):
        self.reset_processed_docs()
        self.reset_loaded_docs()
    
    def _update_vectorstore(self, docs: list):
        if docs:
            processedDocs = set([i.strip() for i in open("./processed.txt")])
            id = int(open("./lastid.txt", mode="r").readline())+1
            for doc in docs:
                semantic_split = []
                len1 = len(processedDocs)
                processedDocs.add(doc.metadata["url"])
                if len(processedDocs) != len1:
                    try:
                        semantic_split = self._semantic_split([doc], last_id=id)
                        if semantic_split[0]:
                            for i in range(len(semantic_split[0])):
                                print(self._vectorstore.add_documents([semantic_split[0][i]], ids=[semantic_split[1][i]]))
                                torch.cuda.empty_cache()
                            id = semantic_split[1][-1]+1
                            Model._toSave.append(doc.metadata["url"])
                            Model._lastId = id
                    except Exception:
                        print("Got an Exception!")
            with open("./lastid.txt", mode="w") as f:
                f.write(f"{id}")

    def update(self, load = True, load_second = True):
        not_loaded = []
        if load:
            Loader.recursive_load(Model._load_path)
        docs = Loader.get_docs_from_table("Processed")
        c = 1
        for doc in docs:
            print(c)
            if not DBInstance.is_in_table("Loaded", doc.metadata["source"], doc.metadata["url"], doc.metadata["title"]):
                not_loaded.append(doc)
            c+=1
        self._update_vectorstore(not_loaded)
        if load_second:
            self._config_load_second_retriever(force_load=True, docs=docs)

    def _get_retriever_config(self):
        config = {"configurable":{}}
        config["configurable"]["vs_r_args"]={}
        for vs_r_field, vs_r_value in Model._retriever_config_fields["vs_r"]:
            config["configurable"]["vs_r_args"][vs_r_value] = self.config_fields[vs_r_field][0]
        for sec_r_f in Model._retriever_config_fields[self.config_fields["second_r_type"][0]]:
            config["configurable"][sec_r_f] = self.config_fields[sec_r_f][0]
        return config

    def remove_doc_from_vectorstore(self, ids):
        self._vectorstore.delete(ids=ids)

    def retrieve(self, query: str) -> dict:
        config = self._get_retriever_config()
        docs = self._ensemble_retriever.invoke(query, config=config)
        result = {"texts":[], "urls":[], "titles":[]}
        ids = []
        for d in docs:
            ids.append(d.metadata["id"])
            result["texts"].append(d.page_content)
            result["urls"].append(d.metadata["url"])
            result["titles"].append(d.metadata["title"])
        print(result)
        print([f"{i}" for i in ids])
        return result
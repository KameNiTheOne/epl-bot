from langchain_core.documents import Document
from langchain_community.document_loaders import TextLoader
from os import walk
from database import DBInstance
class Loader:
    @staticmethod
    def recursive_load(_path: str):
        for path, folders, fnames in walk(_path):
            if folders:
                for folder in folders:
                    Loader.recursive_load(f"{path}\\{folder}")
                return
            pathes = []
            urls = []
            titles = []
            for fpath in fnames:
                pathes.append(f"{path}\\{fpath}")
                with open(f"{path}\\{fpath}", "r", encoding="utf-8") as f:
                    lines = f.readlines()
                    urls.append(lines[0].removesuffix("\n"))
                    titles.append(lines[1].removesuffix("\n"))
            combination = list(zip(pathes, urls, titles))
            for comb in combination:
                DBInstance.is_in_table("Processed", *comb)
    @staticmethod
    def load_from_path(path: str, url: str, title: str):
        doc = TextLoader(path, encoding="utf-8").load()[0]
        doc.page_content = "".join(doc.page_content.splitlines(True)[2:])
        doc.metadata["url"] = url
        doc.metadata["title"] = title
        return doc
    @staticmethod
    def get_docs_from_table(table: str) -> list[Document]:
        saved_docdata = DBInstance.get_all_docdata(table)
        docs = []
        for s in saved_docdata:
            docs.append(Loader.load_from_path(*s))
        return docs
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
from socketserver import ThreadingMixIn
from retrieval import Model
from database import DBInstance
from io import BufferedIOBase
import urllib.parse

class ThreadedHTTPServer(ThreadingMixIn, HTTPServer):
    """Handle requests in a separate thread."""

class Handler(BaseHTTPRequestHandler):
    model: Model | None = None
    def _get_data_from_stream(self, stream: BufferedIOBase, length: int = 0) -> dict:
        print(length)
        temp = stream.read(length)
        temp = temp.decode("utf-8")
        temp = urllib.parse.unquote_plus(temp)
        print(temp)
        return json.loads(temp)

    def _send_txt(self, message, send: bool = False, prnt: bool = True):
        if prnt:
            print(message)
        if send:
            self.send_header("Content-type", "text/text")
            self.end_headers()
            content = bytes(str(message), encoding="UTF-8")
            self.wfile.write(content)

    def _send_json(self, message, prnt: bool = True):
        self._send_txt(json.dumps(message), True, prnt)
    
    def _try_get_content_length(self, headers):
        return int(headers["Content-Length"]) if headers["Content-Length"] != None else int(headers["Length"])

    def do_GET(self):
        # curl http://localhost:8000/<case>
        match(self.path):
            case "/":
                self.send_response(200)
                self._send_txt(f"Model setup? {Handler.model != None}", True)
            case "/info":
                self.send_response(200)
                response = ""
                info_fields = [
                    f"Processed docs amount: {len(DBInstance.get_all_docdata('Processed'))}\n",
                    f"Loaded docs amount: {len(DBInstance.get_all_docdata('Loaded'))}"
                ]
                for key, value in Handler.model.config_fields.items():
                    info_fields.append(f"\n{key}: {value[0]}")
                for f in info_fields:
                    response += f
                self._send_txt(response, True)
            case "/config":
                self.send_response(200)
                response = ""
                config_fields = Handler.model.config_fields
                for f in config_fields.keys():
                    response += f"{f}: {config_fields[f][1]}{config_fields[f][0]}\n"
                response = response.removesuffix("\n")
                self._send_txt(f"Config fields:\n{response}", True)
            case "/update":
                self.send_response(200)
                Handler.model.update(load=False, load_second=False)
                self._send_txt(f"Data update successful", True)
            case "/reset":
                self.send_response(200)
                self._send_txt(f"Reseting db...", True)
                Handler.model.reset_db()
                self._send_txt(f"DB reset successful", True)
            case "/reset/loaded":
                self.send_response(200)
                Handler.model.reset_loaded_docs()
                self._send_txt(f"Loaded reset successful", True)
            case "/reset/processed":
                self.send_response(200)
                Handler.model.reset_processed_docs()
                self._send_txt(f"Processed reset successful", True)
            case _:
                self.send_response(404)
        return
    
    def do_POST(self):
        # curl -X POST -H "Charset: utf-8" -d '{"data":"example"}' http://localhost:8000/<case>
        match(self.path):
            case "/query":
                print(self.headers)
                post_data_str = self._get_data_from_stream(self.rfile, int(self.headers["Content-Length"]))["Value"]
                print (f"MY SERVER: /send:\n{post_data_str}")
                self.send_response(200)

                self._send_json(Handler.model.retrieve(post_data_str), False)
            case "/config":
                post_data = self._get_data_from_stream(self.rfile, int(self.headers["Content-Length"]))
                print (f"MY SERVER: /send:\n{post_data}")
                self.send_response(200)

                Handler.model.config(post_data)
                self._send_txt("Config succesful!", True)
            case "/delete":
                post_data = self._get_data_from_stream(self.rfile, int(self.headers["Content-Length"]))
                print (f"MY SERVER: /send:\n{post_data}")
                self.send_response(200)

                Handler.model.remove_doc_from_vectorstore(list(map(int, post_data["ids"])))
                self._send_txt(f'Deleted docs with these ids: {post_data["ids"]}', True)
            case _:
                self.send_response(404)
        return

class Server:
    def __init__(self, _serverPort = "localhost", _hostName = 8000, handler: Handler = None, load_second_r: bool = True):
        self.instance = ThreadedHTTPServer((_serverPort, _hostName), handler)
        Server._instantiate_model(load_second_r)
        print("Server started http://%s:%s" % (_serverPort, _hostName))
    @staticmethod
    def _instantiate_model(load_second_r):
        if Handler.model == None:
            Handler.model = Model(load_second_r)
    def server_close(self):
        Handler.model.on_close()
        self.instance.server_close()
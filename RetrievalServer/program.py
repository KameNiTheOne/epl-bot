from server import Server, Handler
import socket
if __name__ == "__main__":
    webServer = Server(handler = Handler, _serverPort = socket.gethostbyname(socket.gethostname()), load_second_r=False)
    print(webServer.instance.socket)
    try:
        webServer.instance.serve_forever()
    except KeyboardInterrupt:
        pass

    webServer.server_close()
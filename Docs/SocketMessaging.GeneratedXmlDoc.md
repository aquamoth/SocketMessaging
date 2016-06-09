# SocketMessaging #

## T:Connection

 The Connection wraps a Socket. It is responsible for maintaining the connection and handle the polling logic for the receive buffer. Especially important is to trigger events as messages are received or the connection is closed. The Connection is not meant to be instanced manually but is base class to TcpClient and is contained in the TcpServer's Connections enumeration. 



> While it contains the polling logic in its protected Poll() method Connection is not driving the polling with its own thread. That functionality is delegated to the classes that uses it. 



---
## T:Server.TcpListenerEx

 Wrapper around TcpListener that exposes the Active property See: http://stackoverflow.com/questions/7630094/is-there-a-property-method-for-determining-if-a-tcplistener-is-currently-listeni 



---
#### M:Server.TcpListenerEx.#ctor(System.Net.IPEndPoint)

 Initializes a new instance of the [[|T:System.Net.Sockets.TcpListener]] class with the specified local endpoint. 

|Name | Description |
|-----|------|
|localEP: |An [[|T:System.Net.IPEndPoint]] that represents the local endpoint to which to bind the listener [[|T:System.Net.Sockets.Socket]]. |
[[T:System.ArgumentNullException|T:System.ArgumentNullException]]: |Name | Description |
|-----|------|
|localEP: ||
 is null. 



---
#### M:Server.TcpListenerEx.#ctor(System.Net.IPAddress,System.Int32)

 Initializes a new instance of the [[|T:System.Net.Sockets.TcpListener]] class that listens for incoming connection attempts on the specified local IP address and port number. 

|Name | Description |
|-----|------|
|localaddr: |An [[|T:System.Net.IPAddress]] that represents the local IP address. |
|Name | Description |
|-----|------|
|port: |The port on which to listen for incoming connection attempts. |
[[T:System.ArgumentNullException|T:System.ArgumentNullException]]: |Name | Description |
|-----|------|
|localaddr: ||
 is null. 

[[T:System.ArgumentOutOfRangeException|T:System.ArgumentOutOfRangeException]]: |Name | Description |
|-----|------|
|port: ||
 is not between [[|F:System.Net.IPEndPoint.MinPort]] and [[|F:System.Net.IPEndPoint.MaxPort]]. 



---



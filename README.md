# SocketMessaging
*SocketMessaging* is a package with wrappers around `System.Net.Sockets.Socket`, much like the C# internal classes `TcpClient` and `TcpListener`.
Major benfits over the internal classes are:
* Event-driven design.
* Message-based communication with several supported types of protocols.


##What is it for?
*SocketMessaging* solves two major problems:

1. Event-driven syntax
  * The TcpServer automatically accepts new connections
  * The TcpServer triggers events as clients connect and disconnect
  * Connections automatically poll their receive queue and triggers receive events when new data arrives

2. Message-based communication based on four common configurations.
  * Raw communication of bytes. 
    * No encoding. What you send is what you receive.
    * In this mode you get exactly what Socket gives you, but you still get events when data arrives.
  * Delimiter-based messages
    * Defaults to UTF8-encoded strings, but you choose encoding or send byte-arrays.
    * Supports multi-byte message delimiter. Ending messages with `<CR>` or three dashes is no problem.
    * Can send and receive any message. Delimiters found inside a message are escaped by a customizable escape-code. 
    * The escape-code is itself escaped if found naturally inside the message.
  * Length-prefixed messages
    * A 4-byte Int32 little-endian code prefixes all messages and describes how many bytes of message will follow.
    * Overflow and underflow protection ensures all messages are within reasonable lengths.
  * Fixed-length messages
    * Useful if you know the exact length of one or more messages beforehand and still want receive-events triggered as messages arrives.

	
##Is it resilient to malicious messages?
*SocketMessaging* has a `MaxMessageSize` property which ensures an unreasonably large message isn't retrieved indiscriminately.
`MaxMessageSize` should be set resonably above the max expected message size and will cap retrieval of individual messages.
This of course effectively hangs the communication until the bad bytes are received in raw, but once the receive-buffer is valid again,
receive-events are resumed and messages can be retrieved as normal.

`MaxMessageSize` does not prevent receiving raw bytes from the stream which can be done at any time, event without changing main message mode.

	
##What if I have a complex protocol?
*SocketMessaging* has full support for switching between message modes during communication. 
If your protocol so specifies, you can start in raw mode and then switch to length-prefixed mode, 
only to receive a couple of fixed-length messages before returing to length-prefixed mode.
	

##How is it developed?
*SocketMessaging* is build in *Visual Studio 2015* using C#. 
It is developed in TDD and has a full set of test-coverage written in MsTest.


##How will SocketMessaging evolve?
*SocketMessaging* was sprung from my own need of a simple nuget package to make TCP connections with minimal boilerplate 
and send a couple of messages between a client and a server. As such, I don't intend to widen the envelope much more 
than I currently have. The following items are on my to-do list though (in no particular order):

* Attach/Detach the underlying Socket to TcpServer and TcpClient without closing the current connection.
* Adding async extensions to relevant commands, such as `ConnectAsync` and `SendAsync`.
* UDP-socket wrappers. Since it is an obvious twin to TCP I may include wrappers in the future.

This said, I don't intend *SocketMessaging* to grow into a one-thing-do-all monolith. 
Rather I see other packages may come to require *SocketMessaging* and build on it.

Follow current progress in the [TODO](https://github.com/aquamoth/SocketMessaging/blob/master/TODO.txt) which is always updated.


##What if I find a bug?
Please post bugs under [Issues](https://github.com/aquamoth/SocketMessaging/issues). 
Since you are likely a developer yourself I invite you to solve the bug too and then file a [Pull Request](https://github.com/aquamoth/SocketMessaging/pulls).


#What if I'm missing a feature?
The easiest thing is to file a request under [Issues](https://github.com/aquamoth/SocketMessaging/issues) and then hope for the best.
I make no committment to work on requests on my free time. [Contact me directly](mailto:mattias@trustfall.se) if you want to hire me to do a job.

If you'd like to implement the feature yourself you can either just copy the project to your local computer and stab away or clone this repository on GitHub.
If you complete the feature and think it may benefit others I urge you to isolate it in a feature branch and file a [Pull Request](https://github.com/aquamoth/SocketMessaging/pulls).

Obs! I will only accept pull-requests if:
* I deem the feature of public interest, 
* the code holds good quality, 
* there are accompanying tests that prove the new feature, and 
* **all tests pass**.


##How is SocketMessaging copyrighted and licensed?
*SocketMessaging* is copyright [Trustfall AB, Sweden](http://www.trustfall.se) and licensed under the MIT-license. 
Read the license details in the file [LICENSE](https://github.com/aquamoth/SocketMessaging/blob/master/LICENSE) included in the project.

If the license doesn't work for you, I welcome you to [contact me at mattias@trustfall.se](mailto:mattias@trustfall.se) for alternatives to your liking.


##Contributor License Agreement
You must sign a Contribution License Agreement (CLA) before your PR will be merged. This a one-time requirement for projects copyrighted by Trustfall AB. You can read more about [Contribution License Agreements (CLA)](https://en.wikipedia.org/wiki/Contributor_License_Agreement) on Wikipedia.

However, you don't have to do this up-front. You can simply clone, fork, and submit your pull-request as usual.

When your pull-request is created, it is first classified. If the change is trivial (i.e. you just fixed a typo) then the PR is labelled with `cla-not-required`. Otherwise, it's classified as `cla-required`. In that case, you will receive information on how you sign the CLA. Once you have signed a CLA, the current and all future pull-requests will be labelled as `cla-signed`.

Signing the CLA might sound scary but it's actually very simple and can be done in less than a minute.

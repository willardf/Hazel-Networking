#### Hazel Networking is a low-level networking library for C# providing connection orientated, message based communication via UDP and RUDP.

The aim of this fork is to create a simple interface for ultra-fast connection-based UDP communication for games.

-----

## Features
- UDP and Reliable UDP.
- UDP Broadcast for local-multiplayer.
- Completely thread safe.
- All protocols are connection oriented (similar to TCP) and message based (similar to UDP)
- IPv4 and IPv6 support
- Automatic statistics about data passing in and out of connections
- Designed to be as fast and leightweight as possible

-----

### This fork has been heavily modified from the original to reduce allocations, copies, and locking. As such, it's fairly stable, but not guaranteed. However, my game Among Us currently runs on it with over 200k MAU, so that counts for something.

-----

HTML documentation, tutorials and quickstarts from the DarkRift Website [here](http://www.darkriftnetworking.com/Hazel/Docs) should be relatively accurate; but I doubt the original creator will want support calls for this fork. I can provide some limited support if you create an issue.

I have changed some interfaces in "non-intuitive ways", such as when to recycle MessageReader from DataReceived vs MessageReader.ReadMessage. To be honest, sorry but ¯\_(ツ)_/¯. It's my belief that if a library becomes friendly and proper-API-like, you can expect it to be restrictive or perform slowly. I don't want either of those things, but I try to keep things fairly tidy.

If you want to make improvements, I am open to pull requests. If you find bugs, raise issues.

-----

## Building Hazel

To build Hazel open [solution file](Hazel.sln) using your favourite C# IDE (I use Visual Studio 2017) and then build as you would any other project.

-----

## Tips for using Hazel with Unity

 * Unity doesn't like other threads messing with GameObjects. This isn't a problem for tasks like relaying information. But for tasks like spawning GameObjects on clients or correcting physics, you will want to have a thread safe list of events that are run and cleared during Update or FixedUpdate. 
   * A List<T>+lock(object) is fine because you have many writers, one reader and Hazel doesn't guarantee event order. 
   * A ConcurrentBag is not a bad choice, but you will have to do something special to keep the Update method from hanging if you get an overwhelming number of new events (which suggests problems with your code elsewhere).
 * I also recommend using the ConnectAsync method in a Coroutine that waits for State to change so you don't hang the game while connecting.
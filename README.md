#### Hazel Networking is a low-level networking library for C# providing connection-oriented, message based communication via UDP and RUDP.

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

### This fork has been heavily modified from the original to reduce allocations, copies, and locking. As such, it's fairly stable, but not guaranteed. However, my game Among Us currently runs on it with over 500k MAU, so that counts for something.

-----

HTML documentation, tutorials and quickstarts from the DarkRift Website [here](http://www.darkriftnetworking.com/Hazel/Docs) should be relatively accurate; but I doubt the original creator will want support calls for this fork. I can provide some limited support if you create an issue.

I have changed some interfaces in "non-intuitive ways", it is my hope that [this example repo](https://github.com/willardf/Hazel-Examples) will be able to help users get started.

If you want to make improvements, I am open to pull requests. If you find bugs, feel free raise issues.

-----

## Building Hazel

To build Hazel open [solution file](Hazel.sln) using your favourite C# IDE (I use Visual Studio 2017) and then build as you would any other project.

-----

## Tips for using Hazel with Unity

 * Unity doesn't like other threads messing with GameObjects. This isn't a problem for tasks like relaying information. But for tasks like spawning GameObjects on clients or correcting physics, you will want to have a thread safe list of events that are run and cleared during Update or FixedUpdate. 
   * A List<T>+lock(object) is fine because you have many writers, one reader and Hazel doesn't guarantee event order. 
   * A ConcurrentBag is not a bad choice, but you will have to do something special to keep the Update method from hanging if you get an overwhelming number of new events (which suggests problems with your code elsewhere).
 * I also recommend using the ConnectAsync method in a Coroutine that waits for State to change so you don't hang the game while connecting.

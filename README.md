#### Hazel Networking is a low-level networking library for C# providing connection-oriented, message based communication via UDP and RUDP.

This was originally a fork of a project started by the Dark Rift Networking developer. I stripped that down with the aim of making a simple interface for ultra-fast connection-based UDP communication for games. After Among Us launched, the agreed to make this the primary fork. If you see mention of the original repo, know that it's just a tad out of date.

-----

## Features
- UDP and Reliable UDP.
- Encrypted packets using DTLS
- UDP Broadcast for local-multiplayer.
- Completely thread safe.
- All protocols are connection oriented (similar to TCP) and message based (similar to UDP)
- IPv4 and IPv6 support
- Automatic statistics about data passing in and out of connections
- Designed to be as fast and lightweight as possible

-----

### This fork has been heavily modified from the original to reduce allocations, copies, and locking. It's pretty stable and Among Us uses it for all platforms, but still has the occasional issue.

-----

There is currently no online documentation. I might get around to it someday. I have changed some interfaces in "unintuitive ways", it is my hope that [this example repo](https://github.com/willardf/Hazel-Examples) will be able to help users get started.

If you want to make improvements, I am open to pull requests. If you find bugs, feel free raise issues.

-----

## Installing Hazel
For non unity projects, Hazel can be installed via the NuGet [innersloth.Hazel-Networking](https://www.nuget.org/packages/innersloth.Hazel-Networking) package.

For Unity projects, you'll have to build Hazel first. I recommend just dropping the output DLLs somewhere in the Assets folder.

----

## Building Hazel

To build Hazel open [solution file](Hazel.sln) using your favourite C# IDE (I use Visual Studio 2019) and then build as you would any other project.

-----
## Tips with this fork

 * Pay attention to which callbacks give you ownership of the MessageReader, making you responsible for recycling. In particular:
   * You *should not* recycle messages after NewConnection events.
   * You *should not* recycle messages after Disconnect events.
   * You *should* recycle messages after DataReceived events.
 * Hazel doesn't support fragmented packets. It used to, but I wasn't sure of it so I removed it and have never needed it since. Just stay under 1kb packets.

## Tips for using Hazel with Unity

 * Unity doesn't like other threads messing with GameObjects. This isn't a problem for tasks like relaying information. But for tasks like spawning GameObjects on clients or correcting physics, you will want to have a thread safe list of events that are run and cleared during Update or FixedUpdate. 
   * A List<T>+lock(object) is fine because you have many writers, one reader and Hazel doesn't guarantee event order. 
   * A ConcurrentBag is not a bad choice, but you will have to do something special to keep the Update method from hanging if you get an overwhelming number of new events (which suggests problems with your code elsewhere).
 * I also recommend using the ConnectAsync method in a Coroutine that waits for State to change so you don't hang the game while connecting.

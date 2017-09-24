# Piped Memory Stream

Provides a memory-based two-way stream for inter-thread communication, similar to a socket or a pipe.

This can be used as a communication mechanism, and also as a mock mechanism for testing network 
programs - thread(s) on one side write data and thread(s) on other side receive the data. Whatever 
is written on one end becomes available to read on the other end. 

There are three kinds of abstractions provided:
 - Bidirectional stream: both sides can read and write to theor streams. data written by one side is 
   read by the other side.
 - Unidirectional stream: One side gets a write stream and another side gets a read stream. Data written 
   to the write stream can be read from the read stream.
 - Plain stream: use write() to write to the stream and read() to read from the same stream
 
 
 The undersyling structure is a high-performance circular buffer in memory. the Circular buffer can also 
 be used directly.
 

To use: 
 - Import from NuGet: `Widgeteer.PipedMemoryStream`

In code, use namespace:
 - `using PipedMemoryStream;`
  
Use one of the 4 classes:
 - `ByteBufferBidirectionalStream`
 - `ByteBufferUnidirectionalStream`
 - `ByteBufferStream`
 - `CircularByteBuffer`


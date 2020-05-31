using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using MessagingSample;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MessagingClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = new MessagingSample.MessageSink.MessageSinkClient(channel);

            using var stream = client.Subscribe(new Empty());

            var tokenSource = new CancellationTokenSource();
            var task = DisplayAsync(stream.ResponseStream, tokenSource.Token);

            MessageLoop(client);

            tokenSource.Cancel();

            await task;

        }

        private static void MessageLoop(MessageSink.MessageSinkClient client)
        {
            while (true)
            {
                var msg = Console.ReadLine().TrimEnd();

                if (msg == "quit" || msg == "q")
                {
                    break;
                }

                var request = new PostMessageRequest()
                {
                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                    Message = msg
                };

                client.PostMessage(request);
            }
        }

        private static async Task DisplayAsync(IAsyncStreamReader<MessageSinkUpdate> stream, CancellationToken token)
        {
            try
            {
                await foreach (var update in stream.ReadAllAsync(token))
                {
                    Console.WriteLine($"{update.Time:D} : {update.Message}");
                }
            }
            catch (RpcException e)
            {
                if (e.StatusCode == StatusCode.Cancelled)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Finished.");
            }
        }
    }
}

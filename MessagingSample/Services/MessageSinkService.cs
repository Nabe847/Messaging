using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace MessagingSample
{
    public class MessageSinkService : MessageSink.MessageSinkBase
    {
        private readonly ILogger<MessageSinkService> _logger;

        private MessageSinkObservable observable { get; } = new MessageSinkObservable();

        public MessageSinkService(ILogger<MessageSinkService> logger)
        {
            _logger = logger;
        }

        public async override Task<Empty> PostMessage(PostMessageRequest request, ServerCallContext context)
        {
            var message = new MessageSinkUpdate()
            {
                Time = request.Time,
                Message = request.Message
            };

            this.observable.PublishMessage(message);

            return await Task.FromResult<Empty>(new Empty());
        }

        public async override Task Subscribe(Empty request, IServerStreamWriter<MessageSinkUpdate> responseStream, ServerCallContext context)
        {
            var unsubscriver = this.observable.Subscribe((message) =>
            {
                responseStream.WriteAsync(message);
            });

            var completion = new TaskCompletionSource<object>();
            context.CancellationToken.Register(() =>
            {
                unsubscriver.Dispose();
                completion.SetResult(null);
            });

            await completion.Task;
        }
    }

    public class MessageSinkObservable : IObservable<MessageSinkUpdate>
    {
        private static List<IObserver<MessageSinkUpdate>> observers { get; } = new List<IObserver<MessageSinkUpdate>>();

        public MessageSinkObservable()
        {
            // empty
        }

        public IDisposable Subscribe(IObserver<MessageSinkUpdate> observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }

            return new Unsubscriber<MessageSinkUpdate>(observers, observer);
        }

        public void PublishMessage(MessageSinkUpdate message)
        {
            foreach (var observer in observers)
            {
                observer.OnNext(message);
            }
        }

        public void EndPublish()
        {
            foreach (var observer in observers.ToArray())
            {
                if (observers.Contains(observer))
                {
                    observers.Remove(observer);
                }
            }
        }

        public class Unsubscriber<T> : IDisposable
        {
            private List<IObserver<T>> observers { get; } = new List<IObserver<T>>();
            private IObserver<T> observer;

            public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
            {
                this.observers = observers;
                this.observer = observer;
            }

            public void Dispose()
            {
                if (this.observer != null && this.observers.Contains(this.observer))
                {
                    this.observers.Remove(this.observer);
                }
            }
        }
    }


}

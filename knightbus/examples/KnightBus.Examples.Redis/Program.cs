﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Sagas;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.Redis;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Examples.Redis
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var redisConnection = "string";

            var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
            //Initiate the client
            var client = new RedisBus(new RedisConfiguration(redisConnection));
            client.EnableAttachments(new RedisAttachmentProvider(multiplexer, new RedisConfiguration(redisConnection)));

            var knightBusHost = new KnightBusHost()
                //Enable the Redis Transport
                .UseTransport(new RedisTransport(redisConnection))
                .Configure(configuration => configuration
                    //Enable reading attachments from Redis
                    .UseRedisAttachments(multiplexer, new RedisConfiguration(redisConnection))
                    //Enable the saga store
                    .UseRedisSagaStore(multiplexer, new RedisConfiguration(redisConnection))
                    //Register our message processors without IoC using the standard provider
                    .UseDependencyInjection(new StandardDependecyInjection()
                        .RegisterProcessor(new SampleRedisMessageProcessor())
                        .RegisterProcessor(new SampleRedisAttachmentProcessor())
                        .RegisterProcessor(new RedisEventProcessor())
                        .RegisterProcessor(new RedisEventProcessorTwo())
                        .RegisterProcessor(new RedisEventProcessorThree())
                        .RegisterProcessor(new RedisSagaProcessor(client))
                    )
                    .AddMiddleware(new PerformanceLogging())
                );

            //Start the KnightBus Host, it will now connect to the Redis and listen
            await knightBusHost.StartAsync(CancellationToken.None);

            //Start the saga
            await client.SendAsync(new SampleRedisSagaStarterCommand());
            
            
            //Send some Messages and watch them print in the console
            var messageCount = 10;
            var sw = new Stopwatch();

            var commands = Enumerable.Range(0, messageCount).Select(i => new SampleRedisCommand
            {
                Message = $"Hello from command {i}"
            }).ToList();

            sw.Start();
            await client.SendAsync<SampleRedisCommand>(commands);
            Console.WriteLine($"Elapsed {sw.Elapsed}");
            Console.ReadKey();


            //var attachmentCommands = Enumerable.Range(0, 10).Select(i => new SampleRedisAttachmentCommand()
            //{
            //    Message = $"Hello from command with attachment {i}",
            //    Attachment = new MessageAttachment($"file{i}.txt", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes($"this is a stream from Message {i}")))
            //}).ToList();
            //await client.SendAsync<SampleRedisAttachmentCommand>(attachmentCommands);




            //var events = Enumerable.Range(0, 10).Select(i => new SampleRedisEvent
            //{
            //    Message = $"Hello from event {i}"
            //}).ToList();
            //await client.PublishAsync<SampleRedisEvent>(events);
            Console.ReadKey();

        }

        class SampleRedisCommand : IRedisCommand
        {
            public string Message { get; set; }
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
        }

        class SampleRedisAttachmentCommand : IRedisCommand, ICommandWithAttachment
        {
            public string Message { get; set; }
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
            public IMessageAttachment Attachment { get; set; }
        }

        class SampleRedisEvent : IRedisEvent
        {
            public string Message { get; set; }
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
        }

        class SampleRedisSagaStarterCommand : IRedisCommand
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
            public string SagaId => "9a9f5f4d8abe4c88ad1ba4510f31b605";
        }

        class SampleRedisSagaCommand : IRedisCommand
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
            public string SagaId => "9a9f5f4d8abe4c88ad1ba4510f31b605";
        }

        class SampleRedisSagaStarterCommandMapping : IMessageMapping<SampleRedisSagaStarterCommand>
        {
            public string QueueName => "sample-redis-saga-start-command";
        }

        class SampleRedisSagaCommandMapping : IMessageMapping<SampleRedisSagaCommand>
        {
            public string QueueName => "sample-redis-saga-command";
        }


        class SampleRedisMessageMapping : IMessageMapping<SampleRedisCommand>
        {
            public string QueueName => "sample-redis-command";
        }
        class SampleRedisMessageAttachmentMapping : IMessageMapping<SampleRedisAttachmentCommand>
        {
            public string QueueName => "sample-redis-attachment-command";
        }

        class SampleRedisEventMapping : IMessageMapping<SampleRedisEvent>
        {
            public string QueueName => "sample-redis-event";
        }

        class SampleRedisMessageProcessor : IProcessCommand<SampleRedisCommand, ExtremeRedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisCommand command, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        class SampleRedisAttachmentProcessor : IProcessCommand<SampleRedisAttachmentCommand, RedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisAttachmentCommand command, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Received command: '{command.Message}'");
                using (var streamReader = new StreamReader(command.Attachment.Stream))
                {
                    Console.WriteLine($"Attach file contents:'{streamReader.ReadToEnd()}'");
                }

                return Task.CompletedTask;
            }
        }

        class RedisEventProcessor : IProcessEvent<SampleRedisEvent, EventSubscriptionOne, RedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Handler 1: '{message.Message}'");
                return Task.CompletedTask;
            }
        }
        class RedisEventProcessorTwo : IProcessEvent<SampleRedisEvent, EventSubscriptionTwo, RedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Handler 2: '{message.Message}'");
                return Task.CompletedTask;
            }
        }
        class RedisEventProcessorThree : IProcessEvent<SampleRedisEvent, EventSubscriptionThree, RedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Handler 3: '{message.Message}'");
                return Task.CompletedTask;
            }
        }

        class RedisSagaProcessor : Saga<RedisSagaData>, 
            IProcessCommand<SampleRedisSagaStarterCommand, RedisProcessingSetting>,
            IProcessCommand<SampleRedisSagaCommand, RedisProcessingSetting>
        {
            private readonly IRedisBus _bus;
            public override string PartitionKey => "redis-saga-processor";

            public RedisSagaProcessor(IRedisBus bus)
            {
                _bus = bus;
                //Map messages
                MessageMapper.MapStartMessage<SampleRedisSagaStarterCommand>(m=> m.SagaId);
                MessageMapper.MapMessage<SampleRedisSagaCommand>(m=> m.SagaId);
            }

            public async Task ProcessAsync(SampleRedisSagaStarterCommand message, CancellationToken cancellationToken)
            {
                await _bus.SendAsync(new SampleRedisSagaCommand());
            }

            public async Task ProcessAsync(SampleRedisSagaCommand message, CancellationToken cancellationToken)
            {
                Data.Counter++;
                await UpdateAsync();
                Console.WriteLine($"Saga value was {Data.Counter}");
                if (Data.Counter < 10)
                {
                    await _bus.SendAsync(new SampleRedisSagaCommand());
                }
                else
                {
                    await CompleteAsync();
                    Console.WriteLine("Saga completed");
                }
            }
        }
        class EventSubscriptionOne : IEventSubscription<SampleRedisEvent>
        {
            public string Name => "sub-one";
        }
        class EventSubscriptionTwo : IEventSubscription<SampleRedisEvent>
        {
            public string Name => "sub-two";
        }
        class EventSubscriptionThree : IEventSubscription<SampleRedisEvent>
        {
            public string Name => "sub-three";
        }

        class RedisSagaData
        {
            public int Counter { get; set; }
        }

        public class PerformanceLogging : IMessageProcessorMiddleware
        {
            private int _count;
            private readonly Stopwatch _stopwatch = new Stopwatch();

            public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
            {
                if (!_stopwatch.IsRunning)
                {
                    _stopwatch.Start();
                }
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
                if (++_count % 1000 == 0)
                {
                    Console.WriteLine($"Processed {_count} messages in {_stopwatch.Elapsed} {_count / _stopwatch.Elapsed.TotalSeconds} m/s");
                }
            }
        }

        class ExtremeRedisProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1000;
            public int PrefetchCount => 1000;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 5;
        }
        class RedisProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1;
            public int PrefetchCount => 10;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 5;
        }
    }
}

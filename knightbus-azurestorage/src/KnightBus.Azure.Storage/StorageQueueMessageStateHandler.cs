﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    internal class StorageQueueMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage
    {
        private readonly IStorageQueueClient _queueClient;
        private readonly StorageQueueMessage _message;


        public StorageQueueMessageStateHandler(IStorageQueueClient queueClient, StorageQueueMessage message, int deadLetterDeliveryLimit)
        {
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            _queueClient = queueClient;
            _message = message;
        }

        public int DeliveryCount => _message.DequeueCount;
        public int DeadLetterDeliveryLimit { get; }
        public IDictionary<string, string> MessageProperties => _message.Properties;

        public Task CompleteAsync()
        {
            return _queueClient.CompleteAsync(_message);
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            var errorMessage = e.ToString();
            if (errorMessage.Length > 30000)
            {
                //Messages on the storagebus cannot exceed 64KB. Stay in the safe range without calculating the exact allowed length
                errorMessage = errorMessage.Substring(0, 30000);
            }

            _message.Properties["Error"] = errorMessage;
            return _queueClient.AbandonByErrorAsync(_message, TimeSpan.FromSeconds(2));
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            return _queueClient.DeadLetterAsync(_message);
        }

        public Task<T> GetMessageAsync()
        {
            return Task.FromResult((T)_message.Message);
        }
    }
}
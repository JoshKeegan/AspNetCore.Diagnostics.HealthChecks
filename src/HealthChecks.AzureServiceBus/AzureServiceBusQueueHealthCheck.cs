﻿using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HealthChecks.AzureServiceBus
{
    public class AzureServiceBusQueueHealthCheck
        : IHealthCheck
    {
        private const string TEST_MESSAGE = "HealthCheckTest";
        private static readonly ConcurrentDictionary<string, QueueClient> _queueClientConnections = new ConcurrentDictionary<string, QueueClient>();

        private readonly string _connectionString;
        private readonly string _queueName;
        private readonly Action<Message> _configuringMessage;

        public AzureServiceBusQueueHealthCheck(string connectionString, string queueName, Action<Message> configuringMessage = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
            _configuringMessage = configuringMessage;
        }
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var connectionKey = $"{_connectionString}_{_queueName}";
                if (!_queueClientConnections.TryGetValue(connectionKey, out var queueClient))
                {
                    queueClient = new QueueClient(_connectionString, _queueName,ReceiveMode.PeekLock, RetryPolicy.NoRetry);

                    if (!_queueClientConnections.TryAdd(connectionKey, queueClient))
                    {
                        return new HealthCheckResult(context.Registration.FailureStatus, description: "New QueueClient connection can't be added into dictionary.");
                    }
                }

                var message = new Message(Encoding.UTF8.GetBytes(TEST_MESSAGE));
                _configuringMessage?.Invoke(message);
                
                var scheduledMessageId = await queueClient.ScheduleMessageAsync(message, 
                    new DateTimeOffset(DateTime.UtcNow).AddHours(2));

                await queueClient.CancelScheduledMessageAsync(scheduledMessageId);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
            }
        }
    }
}

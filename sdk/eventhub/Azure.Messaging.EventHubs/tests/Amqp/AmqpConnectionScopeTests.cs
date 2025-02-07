﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Messaging.EventHubs.Amqp;
using Azure.Messaging.EventHubs.Authorization;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.Amqp.Transport;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace Azure.Messaging.EventHubs.Tests
{
    /// <summary>
    ///   The suite of tests for the <see cref="AmqpConnectionScope" />
    ///   class.
    /// </summary>
    ///
    [TestFixture]
    public class AmqpConnectionScopeTests
    {
        /// <summary>
        ///   Verifies functionality of the constructor.
        /// </summary>
        ///
        [Test]
        public void ConstructorValidatesTheEndpoint()
        {
            Assert.That(() => new AmqpConnectionScope(null, "hub", Mock.Of<TokenCredential>(), TransportType.AmqpTcp, null), Throws.ArgumentNullException);
        }

        /// <summary>
        ///   Verifies functionality of the constructor.
        /// </summary>
        ///
        [Test]
        public void ConstructorValidatesTheEventHubName()
        {
            Assert.That(() => new AmqpConnectionScope(new Uri("amqp://some.place.com"), null, Mock.Of<TokenCredential>(), TransportType.AmqpWebSockets, Mock.Of<IWebProxy>()), Throws.ArgumentNullException);
        }

        /// <summary>
        ///   Verifies functionality of the constructor.
        /// </summary>
        ///
        [Test]
        public void ConstructorValidatesTheCredential()
        {
            Assert.That(() => new AmqpConnectionScope(new Uri("amqp://some.place.com"), "hub", null, TransportType.AmqpWebSockets, null), Throws.ArgumentNullException);
        }

        /// <summary>
        ///   Verifies functionality of the constructor.
        /// </summary>
        ///
        [Test]
        public void ConstructorValidatesTheTransport()
        {
            var invalidTransport = (TransportType)(-2);
            Assert.That(() => new AmqpConnectionScope(new Uri("amqp://some.place.com"), "hun", Mock.Of<TokenCredential>(), invalidTransport, Mock.Of<IWebProxy>()), Throws.ArgumentException);
        }

        /// <summary>
        ///   Verifies functionality of the constructor.
        /// </summary>
        ///
        [Test]
        public async Task ConstructorCreatesTheConnection()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            TokenCredential credential = Mock.Of<TokenCredential>();
            TransportType transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection))
                .Verifiable();

            AmqpConnection connection = await GetActiveConnection(mockScope.Object).GetOrCreateAsync(TimeSpan.FromDays(1));
            Assert.That(connection, Is.SameAs(mockConnection), "The connection instance should have been returned");

            mockScope.VerifyAll();
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenManagementLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public void OpenManagementLinkAsyncRespectsTokenCancellation()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            TokenCredential credential = Mock.Of<TokenCredential>();
            TransportType transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            using var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);

            var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Assert.That(() => scope.OpenManagementLinkAsync(TimeSpan.FromDays(1), cancellationSource.Token), Throws.InstanceOf<TaskCanceledException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenManagementLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public void OpenManagementLinkAsyncRespectsDisposal()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            TokenCredential credential = Mock.Of<TokenCredential>();
            TransportType transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);
            scope.Dispose();

            Assert.That(() => scope.OpenManagementLinkAsync(TimeSpan.FromDays(1), CancellationToken.None), Throws.InstanceOf<ObjectDisposedException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenManagementLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenManagementLinkAsyncRequestsTheLink()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            TokenCredential credential = Mock.Of<TokenCredential>();
            TransportType transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());
            var mockLink = new RequestResponseAmqpLink("test", "test", mockSession, "test");

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection))
                .Verifiable();

            mockScope
                .Protected()
                .Setup<Task<RequestResponseAmqpLink>>("CreateManagementLinkAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<TimeSpan>(),
                    ItExpr.Is<CancellationToken>(value => value == cancellationSource.Token))
                .Returns(Task.FromResult(mockLink))
                .Verifiable();

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask)
                .Verifiable();

            var link = await mockScope.Object.OpenManagementLinkAsync(TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.EqualTo(mockLink), "The mock return was incorrect");

            mockScope.VerifyAll();
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenManagementLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenManagementLinkAsyncManagesActiveLinks()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var activeLinks = GetActiveLinks(mockScope.Object);
            Assert.That(activeLinks, Is.Not.Null, "The set of active links was null.");
            Assert.That(activeLinks.Count, Is.Zero, "There should be no active links when none have been created.");

            var link = await mockScope.Object.OpenManagementLinkAsync(TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");

            Assert.That(activeLinks.Count, Is.EqualTo(1), "There should be an active link being tracked.");
            Assert.That(activeLinks.ContainsKey(link), Is.True, "The management link should be tracked as active.");

            activeLinks.TryGetValue(link, out var refreshTimer);
            Assert.That(refreshTimer, Is.Null, "The link should have a null timer since it has no authorization refresh needs.");

            link.SafeClose();
            Assert.That(activeLinks.Count, Is.Zero, "Closing the link should stop tracking it as active.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void OpenConsumerLinkAsyncValidatesTheConsumerGroup(string consumerGroup)
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            using var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);
            Assert.That(() => scope.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), CancellationToken.None), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void OpenConsumerLinkAsyncValidatesThePartitionId(string partitionId)
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "$Default";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            using var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);
            Assert.That(() => scope.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), CancellationToken.None), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public void OpenConsumerLinkAsyncValidatesTheEventPosition()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "$Default";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            using var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);
            Assert.That(() => scope.OpenConsumerLinkAsync(consumerGroup, partitionId, null, options, TimeSpan.FromDays(1), CancellationToken.None), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public void OpenConsumerLinkAsyncValidatesTheConsumerOptions()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "$Default";
            var partitionId = "0";
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            using var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);
            Assert.That(() => scope.OpenConsumerLinkAsync(consumerGroup, partitionId, position, null, TimeSpan.FromDays(1), CancellationToken.None), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public void OpenConsumerLinkAsyncRespectsTokenCancellation()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            using var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);

            var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Assert.That(() => scope.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token), Throws.InstanceOf<TaskCanceledException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public void OpenConsumerLinkAsyncRespectsDisposal()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";

            var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);
            scope.Dispose();

            Assert.That(() => scope.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), CancellationToken.None), Throws.InstanceOf<ObjectDisposedException>());
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenConsumerLinkAsyncRequestsTheLink()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());
            var mockLink = new ReceivingAmqpLink(new AmqpLinkSettings());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection))
                .Verifiable();

            mockScope
                .Protected()
                .Setup<Task<ReceivingAmqpLink>>("CreateReceivingLinkAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.Is<EventPosition>(value => value == position),
                    ItExpr.Is<EventHubConsumerOptions>(value => value == options),
                    ItExpr.IsAny<TimeSpan>(),
                    ItExpr.Is<CancellationToken>(value => value == cancellationSource.Token))
                .Returns(Task.FromResult(mockLink))
                .Verifiable();

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask)
                .Verifiable();

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.EqualTo(mockLink), "The mock return was incorrect");

            mockScope.VerifyAll();
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenConsumerLinkAsyncConfiguresTheLink()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var options = new EventHubConsumerOptions
            {
                Identifier = "testIdentifier123",
                OwnerLevel = 459,
                PrefetchCount = 697,
                TrackLastEnqueuedEventInformation = true
            };

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(1)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");

            var linkSource = (Source)link.Settings.Source;
            Assert.That(linkSource.FilterSet.Any(item => item.Key.Key.ToString() == AmqpFilter.ConsumerFilterName), Is.True, "There should have been a consumer filter set.");
            Assert.That(linkSource.Address.ToString(), Contains.Substring($"/{ partitionId }"), "The partition identifier should have been part of the link address.");
            Assert.That(linkSource.Address.ToString(), Contains.Substring($"/{ consumerGroup }"), "The consumer group should have been part of the link address.");

            Assert.That(link.Settings.TotalLinkCredit, Is.EqualTo((uint)options.PrefetchCount), "The prefetch count should have been used to set the credits.");
            Assert.That(link.Settings.Properties.Any(item => item.Key.Key.ToString() == AmqpProperty.EntityType.ToString()), Is.True, "There should be an entity type specified.");
            Assert.That(link.GetSettingPropertyOrDefault<string>(AmqpProperty.ConsumerIdentifier, null), Is.EqualTo(options.Identifier), "The consumer identifier should have been used.");
            Assert.That(link.GetSettingPropertyOrDefault<long>(AmqpProperty.OwnerLevel, -1), Is.EqualTo(options.OwnerLevel.Value), "The owner level should have been used.");

            Assert.That(link.Settings.DesiredCapabilities, Is.Not.Null, "There should have been a set of desired capabilities created.");
            Assert.That(link.Settings.DesiredCapabilities.Contains(AmqpProperty.TrackLastEnqueuedEventInformation), Is.True, "Last event tracking should be requested.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        [TestCase(null)]
        [TestCase("")]
        public async Task OpenConsumerLinkAsyncRespectsTheIdentifierOption(string consumerIdentifier)
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var options = new EventHubConsumerOptions
            {
                Identifier = consumerIdentifier,
                OwnerLevel = 459,
                PrefetchCount = 697,
                TrackLastEnqueuedEventInformation = true
            };

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(1)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");
            Assert.That(link.GetSettingPropertyOrDefault<string>(AmqpProperty.ConsumerIdentifier, "NONE"), Is.EqualTo("NONE"), "The consumer identifier should not have been set.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenConsumerLinkAsyncRespectsTheOwnerLevelOption()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var options = new EventHubConsumerOptions
            {
                Identifier = "testIdentifier123",
                OwnerLevel = null,
                PrefetchCount = 697,
                TrackLastEnqueuedEventInformation = true
            };

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(1)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");
            Assert.That(link.GetSettingPropertyOrDefault<long>(AmqpProperty.OwnerLevel, long.MinValue), Is.EqualTo(long.MinValue), "The owner level should have been used.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenConsumerLinkAsyncRespectsTheTrackLastEventOption()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var options = new EventHubConsumerOptions
            {
                Identifier = "testIdentifier123",
                OwnerLevel = 9987,
                PrefetchCount = 697,
                TrackLastEnqueuedEventInformation = false
            };

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(1)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");
            Assert.That(link.Settings.DesiredCapabilities, Is.Null, "There should have not have been a set of desired capabilities created, as we're not tracking the last event.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenConsumerLinkAsyncManagesActiveLinks()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(1)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var activeLinks = GetActiveLinks(mockScope.Object);
            Assert.That(activeLinks, Is.Not.Null, "The set of active links was null.");
            Assert.That(activeLinks.Count, Is.Zero, "There should be no active links when none have been created.");

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");

            Assert.That(activeLinks.Count, Is.EqualTo(1), "There should be an active link being tracked.");
            Assert.That(activeLinks.ContainsKey(link), Is.True, "The consumer link should be tracked as active.");

            activeLinks.TryGetValue(link, out var refreshTimer);
            Assert.That(refreshTimer, Is.Not.Null, "The link should have a non-null timer.");

            link.SafeClose();
            Assert.That(activeLinks.Count, Is.Zero, "Closing the link should stop tracking it as active.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenConsumerLinkAsyncConfiguresAuthorizationRefresh()
        {
            var timerCallbackInvoked = false;
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.IsAny<AmqpConnection>(),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.IsAny<Uri>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string[]>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(5)));

            mockScope
                .Protected()
                .Setup<TimerCallback>("CreateAuthorizationRefreshHandler",
                    ItExpr.IsAny<AmqpConnection>(),
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.IsAny<Uri>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string[]>(),
                    ItExpr.IsAny<TimeSpan>(),
                    ItExpr.IsAny<Func<Timer>>())
                .Returns(_ => timerCallbackInvoked = true);

            mockScope
                .Protected()
                .Setup<TimeSpan>("CalculateLinkAuthorizationRefreshInterval",
                    ItExpr.IsAny<DateTime>())
                .Returns(TimeSpan.Zero);

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");

            var activeLinks = GetActiveLinks(mockScope.Object);
            Assert.That(activeLinks.ContainsKey(link), Is.True, "The consumer link should be tracked as active.");

            activeLinks.TryGetValue(link, out var refreshTimer);
            Assert.That(refreshTimer, Is.Not.Null, "The link should have a non-null timer.");

            // The timer be configured to fire immediately and set the flag.  Because the timer
            // runs in the background, there is a level of non-determinism in when that callback will execute.
            // Allow for a small number of delay and retries to account for it.

            var attemptCount = 0;
            var remainingAttempts = 10;

            while ((--remainingAttempts >= 0) && (!timerCallbackInvoked))
            {
                await Task.Delay(250 * ++attemptCount).ConfigureAwait(false);
            }

            Assert.That(timerCallbackInvoked, Is.True, "The timer should have been configured and running when the link was created.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.OpenConsumerLinkAsync" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task OpenConsumerLinkAsyncRefreshesAuthorization()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.IsAny<AmqpConnection>(),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.IsAny<Uri>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string[]>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(5)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The link produced was null");

            var activeLinks = GetActiveLinks(mockScope.Object);
            Assert.That(activeLinks.ContainsKey(link), Is.True, "The consumer link should be tracked as active.");

            activeLinks.TryGetValue(link, out var refreshTimer);
            Assert.That(refreshTimer, Is.Not.Null, "The link should have a non-null timer.");

            // Verify that there was only a initial request for authorization.

            mockScope
                .Protected()
                .Verify("RequestAuthorizationUsingCbsAsync",
                    Times.Once(),
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>());

            // Reset the timer so that it fires immediately and validate that authorization was
            // requested.  Since opening of the link requests an initial authorization and the expiration
            // was set way in the future, there should be exactly two calls.
            //
            // Because the timer runs in the background, there is a level of non-determinism in when that
            // callback will execute.  Allow for a small number of delay and retries to account for it.

            refreshTimer.Change(0, Timeout.Infinite);

            var attemptCount = 0;
            var remainingAttempts = 10;
            var success = false;

            while ((--remainingAttempts >= 0) && (!success))
            {
                try
                {
                    await Task.Delay(250 * ++attemptCount).ConfigureAwait(false);

                    mockScope
                        .Protected()
                        .Verify("RequestAuthorizationUsingCbsAsync",
                            Times.Exactly(2),
                            ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                            ItExpr.IsAny<CbsTokenProvider>(),
                            ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                            ItExpr.IsAny<string>(),
                            ItExpr.IsAny<string>(),
                            ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                            ItExpr.IsAny<TimeSpan>());

                    success = true;
                }
                catch when (remainingAttempts <= 0)
                {
                    throw;
                }
                catch
                {
                    // No action needed.
                }
            }
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.Dispose" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public void DisposeCancelsOperations()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var scope = new AmqpConnectionScope(endpoint, eventHub, credential, transport, null, identifier);
            var cancellation = GetOperationCancellationSource(scope);

            Assert.That(cancellation.IsCancellationRequested, Is.False, "The cancellation source should not be canceled before disposal");

            scope.Dispose();
            Assert.That(cancellation.IsCancellationRequested, Is.True, "The cancellation source should be canceled by disposal");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.Dispose" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task DisposeClosesTheConnection()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            TokenCredential credential = Mock.Of<TokenCredential>();
            TransportType transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var connectionClosed = false;
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());
            var mockLink = new RequestResponseAmqpLink("test", "test", mockSession, "test");

            mockConnection.Closed += (snd, args) => connectionClosed = true;

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection))
                .Verifiable();

            mockScope
                .Protected()
                .Setup<Task<RequestResponseAmqpLink>>("CreateManagementLinkAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<TimeSpan>(),
                    ItExpr.Is<CancellationToken>(value => value == cancellationSource.Token))
                .Returns(Task.FromResult(mockLink))
                .Verifiable();

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Create the mock management link to force lazy creation of the connection.

            await mockScope.Object.OpenManagementLinkAsync(TimeSpan.FromDays(1), cancellationSource.Token);

            mockScope.Object.Dispose();
            Assert.That(connectionClosed, Is.True, "The link should have been closed when the scope was disposed.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.Dispose" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task DisposeClosesActiveLinks()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(1)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var activeLinks = GetActiveLinks(mockScope.Object);
            Assert.That(activeLinks, Is.Not.Null, "The set of active links was null.");
            Assert.That(activeLinks.Count, Is.Zero, "There should be no active links when none have been created.");

            var consumerLink = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(consumerLink, Is.Not.Null, "The consumer link produced was null");

            var managementLink = await mockScope.Object.OpenManagementLinkAsync(TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(managementLink, Is.Not.Null, "The management link produced was null");

            Assert.That(activeLinks.Count, Is.EqualTo(2), "There should be active links being tracked.");
            Assert.That(activeLinks.ContainsKey(managementLink), Is.True, "The management link should be tracked as active.");
            Assert.That(activeLinks.ContainsKey(consumerLink), Is.True, "The consumer link should be tracked as active.");

            mockScope.Object.Dispose();
            Assert.That(activeLinks.Count, Is.Zero, "Disposal should stop tracking it as active.");
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="AmqpConnectionScope.Dispose" />
        ///   method.
        /// </summary>
        ///
        [Test]
        public async Task DisposeStopsManagingLinkAuthorizations()
        {
            var endpoint = new Uri("amqp://test.service.gov");
            var eventHub = "myHub";
            var consumerGroup = "group";
            var partitionId = "0";
            var options = new EventHubConsumerOptions();
            var position = EventPosition.Latest;
            var credential = Mock.Of<TokenCredential>();
            var transport = TransportType.AmqpTcp;
            var identifier = "customIdentIFIER";
            var cancellationSource = new CancellationTokenSource();
            var mockConnection = new AmqpConnection(new MockTransport(), CreateMockAmqpSettings(), new AmqpConnectionSettings());
            var mockSession = new AmqpSession(mockConnection, new AmqpSessionSettings(), Mock.Of<ILinkFactory>());

            var mockScope = new Mock<AmqpConnectionScope>(endpoint, eventHub, credential, transport, null, identifier)
            {
                CallBase = true
            };

            mockScope
                .Protected()
                .Setup<Task<AmqpConnection>>("CreateAndOpenConnectionAsync",
                    ItExpr.IsAny<Version>(),
                    ItExpr.Is<Uri>(value => value == endpoint),
                    ItExpr.Is<TransportType>(value => value == transport),
                    ItExpr.Is<IWebProxy>(value => value == null),
                    ItExpr.Is<string>(value => value == identifier),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(mockConnection));

            mockScope
                .Protected()
                .Setup<Task<DateTime>>("RequestAuthorizationUsingCbsAsync",
                    ItExpr.Is<AmqpConnection>(value => value == mockConnection),
                    ItExpr.IsAny<CbsTokenProvider>(),
                    ItExpr.Is<Uri>(value => value.AbsoluteUri.StartsWith(endpoint.AbsoluteUri)),
                    ItExpr.IsAny<string>(),
                    ItExpr.IsAny<string>(),
                    ItExpr.Is<string[]>(value => value.SingleOrDefault() == EventHubsClaim.Listen),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.FromResult(DateTime.UtcNow.AddDays(1)));

            mockScope
                .Protected()
                .Setup<Task>("OpenAmqpObjectAsync",
                    ItExpr.IsAny<AmqpObject>(),
                    ItExpr.IsAny<TimeSpan>())
                .Returns(Task.CompletedTask);

            var managedAuthorizations = GetActiveLinks(mockScope.Object);
            Assert.That(managedAuthorizations, Is.Not.Null, "The set of managed authorizations was null.");
            Assert.That(managedAuthorizations.Count, Is.Zero, "There should be no managed authorizations when none have been created.");

            var link = await mockScope.Object.OpenConsumerLinkAsync(consumerGroup, partitionId, position, options, TimeSpan.FromDays(1), cancellationSource.Token);
            Assert.That(link, Is.Not.Null, "The consumer link produced was null");

            Assert.That(managedAuthorizations.Count, Is.EqualTo(1), "There should be a managed authorization being tracked.");
            Assert.That(managedAuthorizations.ContainsKey(link), Is.True, "The consumer link should be tracked for authorization.");

            managedAuthorizations.TryGetValue(link, out var refreshTimer);
            Assert.That(refreshTimer, Is.Not.Null, "The link should have a non-null timer.");

            mockScope.Object.Dispose();
            Assert.That(managedAuthorizations.Count, Is.Zero, "Disposal should stop managing authorizations.");
            Assert.That(() => refreshTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan), Throws.InstanceOf<ObjectDisposedException>(), "The timer should have been disposed.");
        }

        /// <summary>
        ///   Gets the active connection for the given scope, using the
        ///   private property accessor.
        /// </summary>
        ///
        private static FaultTolerantAmqpObject<AmqpConnection> GetActiveConnection(AmqpConnectionScope target) =>
            (FaultTolerantAmqpObject<AmqpConnection>)
                typeof(AmqpConnectionScope)
                    .GetProperty("ActiveConnection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty)
                    .GetValue(target);

        /// <summary>
        ///   Gets the set of active links for the given scope, using the
        ///   private property accessor.
        /// </summary>
        ///
        private static ConcurrentDictionary<AmqpObject, Timer> GetActiveLinks(AmqpConnectionScope target) =>
            (ConcurrentDictionary<AmqpObject, Timer>)
                typeof(AmqpConnectionScope)
                    .GetProperty("ActiveLinks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty)
                    .GetValue(target);

        /// <summary>
        ///   Gets the CBS token provider for the given scope, using the
        ///   private property accessor.
        /// </summary>
        ///
        private static CancellationTokenSource GetOperationCancellationSource(AmqpConnectionScope target) =>
            (CancellationTokenSource)
                typeof(AmqpConnectionScope)
                    .GetProperty("OperationCancellationSource", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty)
                    .GetValue(target);

        /// <summary>
        ///   Creates a set of dummy settings for testing purposes.
        /// </summary>
        ///
        private static AmqpSettings CreateMockAmqpSettings()
        {
            var transportProvider = new AmqpTransportProvider();
            transportProvider.Versions.Add(new AmqpVersion(new Version(1, 0, 0, 0)));

            var amqpSettings = new AmqpSettings();
            amqpSettings.TransportProviders.Add(transportProvider);

            return amqpSettings;
        }

        /// <summary>
        ///   Provides a dummy transport for testing purposes.
        /// </summary>
        ///
        private class MockTransport : TransportBase
        {
            public MockTransport() : base("Mock") { }
            public override string LocalEndPoint { get; }
            public override string RemoteEndPoint { get; }
            public override bool ReadAsync(TransportAsyncCallbackArgs args) => throw new NotImplementedException();
            public override void SetMonitor(ITransportMonitor usageMeter) => throw new NotImplementedException();
            public override bool WriteAsync(TransportAsyncCallbackArgs args) => throw new NotImplementedException();
            protected override void AbortInternal() => throw new NotImplementedException();
            protected override bool CloseInternal() => throw new NotImplementedException();
        }
    }
}

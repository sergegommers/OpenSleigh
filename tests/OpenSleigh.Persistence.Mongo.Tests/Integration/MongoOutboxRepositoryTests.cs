﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using OpenSleigh.Core.Exceptions;
using OpenSleigh.Core.Tests.Sagas;
using OpenSleigh.Core.Utils;
using OpenSleigh.Persistence.Mongo.Tests.Fixtures;
using Xunit;

namespace OpenSleigh.Persistence.Mongo.Tests.Integration
{
    [Category("Integration")]
    [Trait("Category", "Integration")]
    public class MongoOutboxRepositoryTests : IClassFixture<DbFixture>
    {
        private readonly DbFixture _fixture;

        public MongoOutboxRepositoryTests(DbFixture fixture)
        {
            _fixture = fixture;
        }
        
        [Fact]
        public async Task LockAsync_should_throw_if_message_not_found()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();
            await Assert.ThrowsAsync<LockException>(async () => await sut.LockAsync(message));
        }

        [Fact]
        public async Task AppendAsync_should_append_message()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();
            await sut.AppendAsync(message);

            var filter = Builders<Entities.OutboxMessage>.Filter.Eq(e => e.Id, message.Id);

            var cursor = await _fixture.DbContext.Outbox.FindAsync(filter);
            var appendedMessage = await cursor.FirstOrDefaultAsync();
            appendedMessage.Should().NotBeNull();
            appendedMessage.LockId.Should().BeNull();
            appendedMessage.LockTime.Should().BeNull();
            appendedMessage.Status.Should().Be("Pending");
        }

        [Fact]
        public async Task LockAsync_should_lock_existing_message()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();
            await sut.AppendAsync(message);

            var lockId = await sut.LockAsync(message);

            var filter = Builders<Entities.OutboxMessage>.Filter.Eq(e => e.Id, message.Id);
            var cursor = await _fixture.DbContext.Outbox.FindAsync(filter);
            var lockedMessage = await cursor.FirstOrDefaultAsync();
            lockedMessage.Should().NotBeNull();
            lockedMessage.LockId.Should().Be(lockId);
            lockedMessage.LockTime.Should().NotBeNull();
        }

        [Fact]
        public async Task LockAsync_should_throw_if_message_already_locked()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();
            await sut.AppendAsync(message);

            await sut.LockAsync(message);

            await Assert.ThrowsAsync<LockException>(async () => await sut.LockAsync(message));
        }

        [Fact]
        public async Task LockAsync_should_throw_if_message_already_processed()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();
            
            await sut.AppendAsync(message);
            var lockId = await sut.LockAsync(message);
            await sut.ReleaseAsync(message, lockId);

            await Assert.ThrowsAsync<LockException>(async () => await sut.LockAsync(message));
        }

        [Fact]
        public async Task ReleaseAsync_should_throw_if_message_not_appended()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentException>(async () => await sut.ReleaseAsync(message, Guid.NewGuid()));
        }

        [Fact]
        public async Task ReleaseAsync_should_throw_if_message_not_locked()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();

            await sut.AppendAsync(message);
            
            await Assert.ThrowsAsync<ArgumentException>(async () => await sut.ReleaseAsync(message, Guid.NewGuid()));
        }

        [Fact]
        public async Task ReleaseAsync_should_throw_if_message_already_locked()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();

            await sut.AppendAsync(message);
            await sut.LockAsync(message);

            await Assert.ThrowsAsync<ArgumentException>(async () => await sut.ReleaseAsync(message, Guid.NewGuid()));
        }

        [Fact]
        public async Task ReleaseAsync_should_update_message_status()
        {
            var message = StartDummySaga.New();
            var sut = CreateSut();

            await sut.AppendAsync(message);
            var lockId = await sut.LockAsync(message);
            await sut.ReleaseAsync(message, lockId);

            var filter = Builders<Entities.OutboxMessage>.Filter.Eq(e => e.Id, message.Id);
            var cursor = await _fixture.DbContext.Outbox.FindAsync(filter);
            var lockedMessage = await cursor.FirstOrDefaultAsync();
            lockedMessage.Status.Should().Be("Processed");
            lockedMessage.LockId.Should().BeNull();
            lockedMessage.LockTime.Should().BeNull();
        }

        [Fact]
        public async Task CleanProcessedAsync_should_clear_processed_messages()
        {
            var sut = CreateSut();
            
            var messages = new[]
            {
                StartDummySaga.New(),
                StartDummySaga.New(),
                StartDummySaga.New()
            };
            foreach(var message in messages)
                await sut.AppendAsync(message);
            
            var processedMessage = StartDummySaga.New();

            await sut.AppendAsync(processedMessage);
            var lockId = await sut.LockAsync(processedMessage);
            await sut.ReleaseAsync(processedMessage, lockId);

            var filter = Builders<Entities.OutboxMessage>.Filter.Eq(e => e.Id, processedMessage.Id);
            var count = await _fixture.DbContext.Outbox.CountDocumentsAsync(filter);
            count.Should().Be(1);

            await sut.CleanProcessedAsync();

            count = await _fixture.DbContext.Outbox.CountDocumentsAsync(filter);
            count.Should().Be(0);
        }

        private MongoOutboxRepository CreateSut(MongoOutboxRepositoryOptions options = null)
        {
            var serializer = new JsonSerializer();

            options ??= MongoOutboxRepositoryOptions.Default;

            var sut = new MongoOutboxRepository(_fixture.DbContext, serializer, options);
            return sut;
        }

    }
}

﻿using Hashgraph.Test.Fixtures;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Hashgraph.Test.Topic
{
    [Collection(nameof(NetworkCredentials))]
    public class UpdateTopicTests
    {
        private readonly NetworkCredentials _network;
        public UpdateTopicTests(NetworkCredentials network, ITestOutputHelper output)
        {
            _network = network;
            _network.Output = output;
        }
        [Fact(DisplayName = "Update Topic: Call to Update Topic without Topic ID Raises Error")]
        public async Task UpdateWitnoutTopicRaisesError()
        {
            await using var fx = await TestTopic.CreateAsync(_network);
            var newMemo = Generator.String(10, 100);
            var ane = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await fx.Client.UpdateTopicAsync(new UpdateTopicParams
                {
                    Signatory = fx.AdminPrivateKey,
                    Memo = newMemo,
                });
            });
            Assert.Equal("Topic", ane.ParamName);
            Assert.StartsWith("Topic address is missing", ane.Message);
        }
        [Fact(DisplayName = "Update Topic: Call to Update With No Changes Raises Error")]
        public async Task UpdateWitnoutChangesRaisesError()
        {
            await using var fx = await TestTopic.CreateAsync(_network);
            var ae = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await fx.Client.UpdateTopicAsync(new UpdateTopicParams
                {
                    Topic = fx.Record.Topic,
                    Signatory = fx.AdminPrivateKey
                });
            });
            Assert.Equal("updateParameters", ae.ParamName);
            Assert.StartsWith("The Topic Updates contain no update properties, it is blank", ae.Message);
        }
        [Fact(DisplayName = "Update Topic: Can Update Memo")]
        public async Task CanUpdateMemo()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            var newMemo = Generator.String(10, 100);
            var receipt = await fx.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                Memo = newMemo,
            });
            Assert.Equal(ResponseCode.Success, receipt.Status);

            var info = await fx.Client.GetTopicInfoAsync(fx.Record.Topic);
            Assert.Equal(newMemo, info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Equal(new Endorsement(fx.AdminPublicKey), info.Administrator);
            Assert.Equal(new Endorsement(fx.ParticipantPublicKey), info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Equal(fx.TestAccount.Record.Address, info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Can Update Memo (With Record)")]
        public async Task CanUpdateMemoWithRecord()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            var newMemo = Generator.String(10, 100);
            var record = await fx.Client.UpdateTopicWithRecordAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                Memo = newMemo,
            });
            Assert.Equal(ResponseCode.Success, record.Status);
            Assert.False(record.Hash.IsEmpty);
            Assert.NotNull(record.Concensus);
            Assert.Empty(record.Memo);
            Assert.InRange(record.Fee, 0UL, ulong.MaxValue);
            Assert.Equal(_network.Payer, record.Id.Address);

            var info = await fx.Client.GetTopicInfoAsync(fx.Record.Topic);
            Assert.Equal(newMemo, info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Equal(new Endorsement(fx.AdminPublicKey), info.Administrator);
            Assert.Equal(new Endorsement(fx.ParticipantPublicKey), info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Equal(fx.TestAccount.Record.Address, info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Can Update Memo to Empty")]
        public async Task CanUpdateMemoToEmpty()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            await fx.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                Memo = string.Empty,
            });

            var info = await fx.Client.GetTopicInfoAsync(fx.Record.Topic);
            Assert.Empty(info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Equal(new Endorsement(fx.AdminPublicKey), info.Administrator);
            Assert.Equal(new Endorsement(fx.ParticipantPublicKey), info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Equal(fx.TestAccount.Record.Address, info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Removing Administrator without removing Auto Renew Account Raises Error")]
        public async Task RemovingAdministratorWithoutRemovingAutoRenewAccountRaisesError()
        {
            await using var fx = await TestTopic.CreateAsync(_network);
            var tex = await Assert.ThrowsAsync<TransactionException>(async () =>
            {
                await fx.Client.UpdateTopicAsync(new UpdateTopicParams
                {
                    Topic = fx.Record.Topic,
                    Signatory = fx.AdminPrivateKey,
                    Administrator = Endorsement.None
                });
            });
            Assert.Equal(ResponseCode.AutorenewAccountNotAllowed, tex.Status);
            Assert.StartsWith("Unable to update Topic, status: AutorenewAccountNotAllowed", tex.Message);
        }
        [Fact(DisplayName = "Update Topic: Can Update Administrator to None (Make Imutable)")]
        public async Task CanMakeImutable()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            await fx.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                Administrator = Endorsement.None,
                RenewAccount = Address.None
            });

            var info = await fx.Client.GetTopicInfoAsync(fx.Record.Topic);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Null(info.Administrator);
            Assert.Equal(new Endorsement(fx.ParticipantPublicKey), info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Null(info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Can't Update After Made Imutable")]
        public async Task CannotUpdateAfterMadeImmutable()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            await fx.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                Administrator = Endorsement.None,
                RenewAccount = Address.None
            });

            var tex = await Assert.ThrowsAsync<TransactionException>(async () =>
            {
                await fx.Client.UpdateTopicAsync(new UpdateTopicParams
                {
                    Topic = fx.Record.Topic,
                    Signatory = fx.AdminPrivateKey,
                    Memo = Generator.String(10, 100)
                });
            });
            Assert.Equal(ResponseCode.Unauthorized, tex.Status);
            Assert.StartsWith("Unable to update Topic, status: Unauthorized", tex.Message);
        }
        [Fact(DisplayName = "Update Topic: Can Update Participant")]
        public async Task CanUpdateParticipant()
        {
            await using var fx = await TestTopic.CreateAsync(_network);
            var (newPublic, _) = Generator.KeyPair();

            await fx.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                Participant = newPublic
            });

            var info = await fx.Client.GetTopicInfoAsync(fx.Record.Topic);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Equal(new Endorsement(fx.AdminPublicKey), info.Administrator);
            Assert.Equal(new Endorsement(newPublic), info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Equal(fx.TestAccount.Record.Address, info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Can Update Participant to None")]
        public async Task CanUpdateParticipantToNone()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            await fx.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                Participant = Endorsement.None
            });

            var info = await fx.Client.GetTopicInfoAsync(fx.Record.Topic);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Equal(new Endorsement(fx.AdminPublicKey), info.Administrator);
            Assert.Null(info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Equal(fx.TestAccount.Record.Address, info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Update Renew Period to Invlid Raises Error")]
        public async Task CanUpdateRenewPeriod()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            var tex = await Assert.ThrowsAsync<TransactionException>(async () =>
            {
                await fx.Client.UpdateTopicAsync(new UpdateTopicParams
                {
                    Topic = fx.Record.Topic,
                    Signatory = fx.AdminPrivateKey,
                    RenewPeriod = TimeSpan.FromDays(1)
                });
            });
            Assert.Equal(ResponseCode.AutorenewDurationNotInRange, tex.Status);
            Assert.StartsWith("Unable to update Topic, status: AutorenewDurationNotInRange", tex.Message);
        }
        [Fact(DisplayName = "Update Topic: Can Update Auto Renew Account")]
        public async Task CanUpdateAutoRenewAccount()
        {
            await using var fxTopic = await TestTopic.CreateAsync(_network);
            await using var fxAccount = await TestAccount.CreateAsync(_network);

            await fxTopic.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fxTopic.Record.Topic,
                Signatory = new Signatory(fxTopic.AdminPrivateKey, fxAccount.PrivateKey),
                RenewAccount = fxAccount.Record.Address
            });

            var info = await fxTopic.Client.GetTopicInfoAsync(fxTopic.Record.Topic);
            Assert.Equal(fxTopic.Memo, info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Equal(new Endorsement(fxTopic.AdminPublicKey), info.Administrator);
            Assert.Equal(new Endorsement(fxTopic.ParticipantPublicKey), info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Equal(fxAccount.Record.Address, info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Can Update Auto Renew Account to None")]
        public async Task CanUpdateAutoRenewAccountToNone()
        {
            await using var fx = await TestTopic.CreateAsync(_network);

            await fx.Client.UpdateTopicAsync(new UpdateTopicParams
            {
                Topic = fx.Record.Topic,
                Signatory = fx.AdminPrivateKey,
                RenewAccount = Address.None
            });

            var info = await fx.Client.GetTopicInfoAsync(fx.Record.Topic);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.NotEqual(ReadOnlyMemory<byte>.Empty, info.RunningHash);
            Assert.Equal(0UL, info.SequenceNumber);
            Assert.True(info.Expiration > DateTime.MinValue);
            Assert.Equal(new Endorsement(fx.AdminPublicKey), info.Administrator);
            Assert.Equal(new Endorsement(fx.ParticipantPublicKey), info.Participant);
            Assert.True(info.AutoRenewPeriod > TimeSpan.MinValue);
            Assert.Null(info.RenewAccount);
        }
        [Fact(DisplayName = "Update Topic: Need Admin Signature")]
        public async Task NeedsAdminSignature()
        {
            await using var fx = await TestTopic.CreateAsync(_network);
            var newMemo = Generator.String(10, 100);

            var tex = await Assert.ThrowsAsync<TransactionException>(async () =>
            {
                await fx.Client.UpdateTopicAsync(new UpdateTopicParams
                {
                    Topic = fx.Record.Topic,
                    Memo = newMemo,
                });
            });
            Assert.Equal(ResponseCode.InvalidSignature, tex.Status);
            Assert.StartsWith("Unable to update Topic, status: InvalidSignature", tex.Message);
        }
    }
}

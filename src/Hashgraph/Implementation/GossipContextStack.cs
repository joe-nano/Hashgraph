﻿using Google.Protobuf;
using Grpc.Core;
using System;

namespace Hashgraph.Implementation
{
    /// <summary>
    /// Internal Implementation of the <see cref="IContext"/> used for configuring
    /// <see cref="Client"/> objects.  Maintains a stack of parent objects 
    /// and coordinates values returned for various contexts.  Not intended for
    /// public use.
    /// </summary>
    internal class GossipContextStack : ContextStack<GossipContextStack>, IContext
    {
        public Gateway? Gateway { get => get<Gateway>(nameof(Gateway)); set => set(nameof(Gateway), value); }
        public Address? Payer { get => get<Address>(nameof(Payer)); set => set(nameof(Payer), value); }
        public Signatory? Signatory { get => get<Signatory>(nameof(Signatory)); set => set(nameof(Signatory), value); }
        public long FeeLimit { get => get<long>(nameof(FeeLimit)); set => set(nameof(FeeLimit), value); }
        public TimeSpan TransactionDuration { get => get<TimeSpan>(nameof(TransactionDuration)); set => set(nameof(TransactionDuration), value); }
        public int RetryCount { get => get<int>(nameof(RetryCount)); set => set(nameof(RetryCount), value); }
        public TimeSpan RetryDelay { get => get<TimeSpan>(nameof(RetryDelay)); set => set(nameof(RetryDelay), value); }
        public string? Memo { get => get<string>(nameof(Memo)); set => set(nameof(Memo), value); }
        public bool AdjustForLocalClockDrift { get => get<bool>(nameof(AdjustForLocalClockDrift)); set => set(nameof(AdjustForLocalClockDrift), value); }
        public TxId? Transaction { get => get<TxId>(nameof(Transaction)); set => set(nameof(Transaction), value); }
        public Action<IMessage>? OnSendingRequest { get => get<Action<IMessage>>(nameof(OnSendingRequest)); set => set(nameof(OnSendingRequest), value); }
        public Action<int, IMessage>? OnResponseReceived { get => get<Action<int, IMessage>>(nameof(OnResponseReceived)); set => set(nameof(OnResponseReceived), value); }

        public GossipContextStack(GossipContextStack? parent) : base(parent) { }
        protected override bool IsValidPropertyName(string name)
        {
            switch (name)
            {
                case nameof(Gateway):
                case nameof(Payer):
                case nameof(Signatory):
                case nameof(FeeLimit):
                case nameof(RetryCount):
                case nameof(RetryDelay):
                case nameof(TransactionDuration):
                case nameof(Memo):
                case nameof(AdjustForLocalClockDrift):
                case nameof(Transaction):
                case nameof(OnSendingRequest):
                case nameof(OnResponseReceived):
                    return true;
                default:
                    return false;
            }
        }
        protected override string GetChannelUrl()
        {
            var url = Gateway?.Url;
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("The Network Gateway Node has not been configured.");
            }
            return url;
        }
        protected override Channel ConstructNewChannel(string url)
        {
            return new Channel(url, ChannelCredentials.Insecure);
        }
    }
}

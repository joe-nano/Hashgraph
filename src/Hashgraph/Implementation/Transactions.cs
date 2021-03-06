﻿using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hashgraph.Implementation;
using Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hashgraph
{
    /// <summary>
    /// Internal helper class providing protobuf message construction 
    /// helpers and communication with the remote GRPC server.
    /// </summary>
    internal static class Transactions
    {
        internal static Signatory GatherSignatories(GossipContextStack context, params Signatory?[] extraSignatories)
        {
            var signatories = new List<Signatory>(1 + extraSignatories.Length);
            var contextSignatory = context.Signatory;
            if (!(contextSignatory is null))
            {
                signatories.Add(contextSignatory);
            }
            foreach (var extraSignatory in extraSignatories)
            {
                if (!(extraSignatory is null))
                {
                    signatories.Add(extraSignatory);
                }
            }
            return signatories.Count == 1 ?
                signatories[0] :
                new Signatory(signatories.ToArray());
        }
        internal static TransactionID GetOrCreateTransactionID(GossipContextStack context)
        {
            var preExistingTransaction = context.Transaction;
            if (preExistingTransaction is null)
            {
                var (seconds, nanos) = Epoch.UniqueSecondsAndNanos(context.AdjustForLocalClockDrift);
                return new TransactionID
                {
                    AccountID = Protobuf.ToAccountID(RequireInContext.Payer(context)),
                    TransactionValidStart = new Proto.Timestamp
                    {
                        Seconds = seconds,
                        Nanos = nanos
                    }
                };
            }
            else
            {
                return Protobuf.ToTransactionID(preExistingTransaction);
            }
        }
        internal static TransferList CreateCryptoTransferList(params (Address address, long amount)[] list)
        {
            var netRequests = new Dictionary<Address, long>();
            foreach (var transfer in list)
            {
                if (netRequests.TryGetValue(transfer.address, out long value))
                {
                    netRequests[transfer.address] = value + transfer.amount;
                }
                else
                {
                    netRequests[transfer.address] = transfer.amount;
                }
            }
            var transfers = new TransferList();
            foreach (var transfer in netRequests)
            {
                if (transfer.Value != 0)
                {
                    transfers.AccountAmounts.Add(new AccountAmount
                    {
                        AccountID = Protobuf.ToAccountID(transfer.Key),
                        Amount = transfer.Value
                    });
                }
            }
            return transfers;
        }
        internal static TransactionBody CreateTransactionBody(GossipContextStack context, TransactionID transactionId)
        {
            return new TransactionBody
            {
                TransactionID = transactionId,
                NodeAccountID = Protobuf.ToAccountID(RequireInContext.Gateway(context)),
                TransactionFee = (ulong)context.FeeLimit,
                TransactionValidDuration = Protobuf.ToDuration(context.TransactionDuration),
                Memo = context.Memo ?? ""
            };
        }
        internal static QueryHeader CreateAskCostHeader()
        {
            return new QueryHeader
            {
                Payment = new Transaction { BodyBytes = ByteString.Empty },
                ResponseType = ResponseType.CostAnswer
            };
        }
        internal static async Task<QueryHeader> CreateAndSignQueryHeaderAsync(GossipContextStack context, long queryFee, TransactionID transactionId)
        {
            var gateway = RequireInContext.Gateway(context);
            var payer = RequireInContext.Payer(context);
            var signatory = RequireInContext.Signatory(context);
            var fee = RequireInContext.QueryFee(context, queryFee);
            TransactionBody transactionBody = new TransactionBody
            {
                TransactionID = transactionId,
                NodeAccountID = Protobuf.ToAccountID(gateway),
                TransactionFee = (ulong)context.FeeLimit,
                TransactionValidDuration = Protobuf.ToDuration(context.TransactionDuration),
                Memo = context.Memo ?? ""
            };
            var transfers = CreateCryptoTransferList((payer, -fee), (gateway, fee));
            transactionBody.CryptoTransfer = new CryptoTransferTransactionBody { Transfers = transfers };
            return new QueryHeader
            {
                Payment = await SignTransactionAsync(transactionBody, signatory)
            };
        }
        internal static async Task<Proto.Transaction> SignTransactionAsync(TransactionBody transactionBody, ISignatory signatory)
        {
            var invoice = new Invoice(transactionBody);
            await signatory.SignAsync(invoice);
            return invoice.GetSignedTransaction();
        }
        internal async static Task<TResponse> ExecuteUnsignedAskRequestWithRetryAsync<TRequest, TResponse>(GossipContextStack context, TRequest request, Func<Channel, Func<TRequest, Task<TResponse>>> instantiateRequestMethod, Func<TResponse, ResponseHeader?> getResponseHeader) where TRequest : IMessage where TResponse : IMessage
        {
            var answer = await ExecuteNetworkRequestWithRetryAsync(context, request, instantiateRequestMethod, shouldRetryRequest);
            var code = getResponseHeader(answer)?.NodeTransactionPrecheckCode ?? ResponseCodeEnum.Unknown;
            if (code != ResponseCodeEnum.Ok)
            {
                throw new PrecheckException($"Transaction Failed Pre-Check: {code}", new TxId(), (ResponseCode)code, 0);
            }
            return answer;

            bool shouldRetryRequest(TResponse response)
            {
                return ResponseCodeEnum.Busy == getResponseHeader(response)?.NodeTransactionPrecheckCode;
            }
        }

        internal static Task<TResponse> ExecuteSignedRequestWithRetryAsync<TRequest, TResponse>(GossipContextStack context, TRequest request, Func<Channel, Func<TRequest, Task<TResponse>>> instantiateRequestMethod, Func<TResponse, ResponseHeader?> getResponseHeader) where TRequest : IMessage where TResponse : IMessage
        {
            return ExecuteSignedRequestWithRetryAsync(context, request, instantiateRequestMethod, getResponseCode);

            ResponseCodeEnum getResponseCode(TResponse response)
            {
                return getResponseHeader(response)?.NodeTransactionPrecheckCode ?? ResponseCodeEnum.Unknown;
            }
        }

        internal static Task<TResponse> ExecuteSignedRequestWithRetryAsync<TRequest, TResponse>(GossipContextStack context, TRequest request, Func<Channel, Func<TRequest, Task<TResponse>>> instantiateRequestMethod, Func<TResponse, ResponseCodeEnum> getResponseCode) where TRequest : IMessage where TResponse : IMessage
        {
            var trackTimeDrift = context.AdjustForLocalClockDrift && context.Transaction is null;
            var startingInstant = trackTimeDrift ? Epoch.UniqueClockNanos() : 0;

            return ExecuteNetworkRequestWithRetryAsync(context, request, instantiateRequestMethod, shouldRetryRequest);

            bool shouldRetryRequest(TResponse response)
            {
                var code = getResponseCode(response);
                if (trackTimeDrift && code == ResponseCodeEnum.InvalidTransactionStart)
                {
                    Epoch.AddToClockDrift(Epoch.UniqueClockNanos() - startingInstant);
                }
                return
                    code == ResponseCodeEnum.Busy ||
                    code == ResponseCodeEnum.InvalidTransactionStart;
            }
        }
        internal static async Task<TResponse> ExecuteNetworkRequestWithRetryAsync<TRequest, TResponse>(GossipContextStack context, TRequest request, Func<Channel, Func<TRequest, Task<TResponse>>> instantiateRequestMethod, Func<TResponse, bool> shouldRetryRequest) where TRequest : IMessage where TResponse : IMessage
        {
            try
            {
                var retryCount = 0;
                var maxRetries = context.RetryCount;
                var retryDelay = context.RetryDelay;
                var callOnSendingHandlers = InstantiateOnSendingRequestHandler(context);
                var callOnResponseReceivedHandlers = InstantiateOnResponseReceivedHandler(context);
                var sendRequest = instantiateRequestMethod(context.GetChannel());
                callOnSendingHandlers(request);
                for (; retryCount < maxRetries; retryCount++)
                {
                    try
                    {
                        var tenativeResponse = await sendRequest(request);
                        callOnResponseReceivedHandlers(retryCount, tenativeResponse);
                        if (!shouldRetryRequest(tenativeResponse))
                        {
                            return tenativeResponse;
                        }
                    }
                    catch (RpcException rpcex) when (rpcex.StatusCode == StatusCode.Unavailable)
                    {
                        var channel = context.GetChannel();
                        var message = channel.State == ChannelState.Connecting ?
                            $"Unable to communicate with network node {channel.ResolvedTarget}, it may be down or not reachable." :
                            $"Unable to communicate with network node {channel.ResolvedTarget}: {rpcex.Status}";
                        callOnResponseReceivedHandlers(retryCount, new StringValue { Value = message });

                        // If this was a transaction, it may have actully successfully been processed, in which case 
                        // the receipt will already be in the system.  Check to see if it is there.
                        if (request is Transaction transaction)
                        {
                            await Task.Delay(retryDelay * retryCount);
                            var receiptResponse = await CheckForReceipt(transaction);
                            callOnResponseReceivedHandlers(retryCount, receiptResponse);
                            if (receiptResponse.NodeTransactionPrecheckCode != ResponseCodeEnum.ReceiptNotFound &&
                                receiptResponse is TResponse tenativeResponse &&
                                !shouldRetryRequest(tenativeResponse))
                            {
                                return tenativeResponse;
                            }
                        }
                    }
                    await Task.Delay(retryDelay * (retryCount + 1));
                }
                var finalResponse = await sendRequest(request);
                callOnResponseReceivedHandlers(maxRetries, finalResponse);
                return finalResponse;

                async Task<TransactionResponse> CheckForReceipt(Transaction transaction)
                {
                    // In the case we submitted a transaction, the receipt may actually
                    // be in the system.  Unpacking the transaction is not necessarily efficient,
                    // however we are here due to edge case error condition due to poor network 
                    // performance or grpc connection issues already.
                    if (transaction != null)
                    {
                        var transactionBody = TransactionBody.Parser.ParseFrom(transaction.BodyBytes);
                        var transactionId = transactionBody.TransactionID;
                        var query = new Query
                        {
                            TransactionGetReceipt = new TransactionGetReceiptQuery
                            {
                                TransactionID = transactionId
                            }
                        };
                        for (; retryCount < maxRetries; retryCount++)
                        {
                            try
                            {
                                var client = new CryptoService.CryptoServiceClient(context.GetChannel());
                                var receipt = await client.getTransactionReceiptsAsync(query);
                                return new TransactionResponse { NodeTransactionPrecheckCode = receipt.TransactionGetReceipt.Header.NodeTransactionPrecheckCode };
                            }
                            catch (RpcException rpcex) when (rpcex.StatusCode == StatusCode.Unavailable)
                            {
                                var channel = context.GetChannel();
                                var message = channel.State == ChannelState.Connecting ?
                                    $"Unable to communicate with network node {channel.ResolvedTarget}, it may be down or not reachable." :
                                    $"Unable to communicate with network node {channel.ResolvedTarget}: {rpcex.Status}";
                                callOnResponseReceivedHandlers(retryCount, new StringValue { Value = message });
                            }
                            await Task.Delay(retryDelay * (retryCount + 1));
                        }
                    }
                    return new TransactionResponse { NodeTransactionPrecheckCode = ResponseCodeEnum.Unknown };
                }
            }
            catch (RpcException rpcex)
            {
                var channel = context.GetChannel();
                var message = rpcex.StatusCode == StatusCode.Unavailable && channel.State == ChannelState.Connecting ?
                    $"Unable to communicate with network node {channel.ResolvedTarget}, it may be down or not reachable." :
                    $"Unable to communicate with network node {channel.ResolvedTarget}: {rpcex.Status}";
                throw new PrecheckException(message, new TxId(), ResponseCode.Unknown, 0, rpcex);
            }
        }

        private static Action<IMessage> InstantiateOnSendingRequestHandler(GossipContextStack context)
        {
            var handlers = context.GetAll<Action<IMessage>>(nameof(context.OnSendingRequest)).Where(h => h != null).ToArray();
            if (handlers.Length > 0)
            {
                return (IMessage request) => ExecuteHandlers(handlers, request);
            }
            else
            {
                return NoOp;
            }
            static void ExecuteHandlers(Action<IMessage>[] handlers, IMessage request)
            {
                var data = new ReadOnlyMemory<byte>(request.ToByteArray());
                foreach (var handler in handlers)
                {
                    handler(request);
                }
            }
            static void NoOp(IMessage request)
            {
            }
        }
        private static Action<int, IMessage> InstantiateOnResponseReceivedHandler(GossipContextStack context)
        {
            var handlers = context.GetAll<Action<int, IMessage>>(nameof(context.OnResponseReceived)).Where(h => h != null).ToArray();
            if (handlers.Length > 0)
            {
                return (int tryNumber, IMessage response) => ExecuteHandlers(handlers, tryNumber, response);
            }
            else
            {
                return NoOp;
            }
            static void ExecuteHandlers(Action<int, IMessage>[] handlers, int tryNumber, IMessage response)
            {
                foreach (var handler in handlers)
                {
                    handler(tryNumber, response);
                }
            }
            static void NoOp(int tryNumber, IMessage response)
            {
            }
        }
    }
}

﻿using Grpc.Core;
using Hashgraph.Implementation;
using Proto;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Hashgraph
{
    public partial class Client
    {
        public async Task<TransferRecord> TransferAsync(Account fromAccount, Address toAddress, long amount, Action<IContext>? configure = null)
        {
            Require.FromAccountArgument(fromAccount);
            Require.ToAddressArgument(toAddress);
            Require.AmountArgument(amount);
            var context = CreateChildContext(configure);
            Require.GatewayInContext(context);
            var payer = Require.PayerInContext(context);
            var transfers = Transactions.CreateCryptoTransferList((fromAccount, -amount), (toAddress, amount));
            var transactionId = Transactions.GetOrCreateTransactionID(context);
            var transactionBody = Transactions.CreateCryptoTransferTransactionBody(context, transfers, transactionId, "Transfer Crypto");
            var signatures = Transactions.SignProtoTransactionBody(transactionBody, payer, fromAccount);
            var request = new Transaction
            {
                Body = transactionBody,
                Sigs = signatures
            };
            var response = await Transactions.ExecuteRequestWithRetryAsync(context, request, instantiateExecuteCryptoGetBalanceAsyncMethod, checkForRetry);
            Validate.ValidatePreCheckResult(transactionId, response.NodeTransactionPrecheckCode);
            var record = await GetFastRecordAsync(transactionId, context);
            if (record.Receipt.Status != ResponseCodeEnum.Success)
            {
                throw new TransactionException($"Unable to execute crypto transfer, status: {record.Receipt.Status}", Protobuf.FromTransactionRecord<TransactionRecord>(record, transactionId));
            }
            var result = Protobuf.FromTransactionRecord<TransferRecord>(record, transactionId);
            result.Transfers = Protobuf.FromTransferList(record.TransferList);
            return result;

            static Func<Transaction, Task<TransactionResponse>> instantiateExecuteCryptoGetBalanceAsyncMethod(Channel channel)
            {
                var client = new CryptoService.CryptoServiceClient(channel);
                return async (Transaction request) => await client.cryptoTransferAsync(request);
            }

            static bool checkForRetry(TransactionResponse response)
            {
                var code = response.NodeTransactionPrecheckCode;
                return
                    code == ResponseCodeEnum.Busy ||
                    code == ResponseCodeEnum.InvalidTransactionStart;
            }
        }
#pragma warning disable CS8618 // Non-nullable field is uninitialized.
        public class TransferRecord : TransactionRecord
        {
            public ReadOnlyDictionary<Address, long> Transfers { get; internal set; }
        }
#pragma warning restore CS8618 // Non-nullable field is uninitialized.
    }
}
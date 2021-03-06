﻿using Grpc.Core;
using Hashgraph.Implementation;
using Proto;
using System;
using System.Threading.Tasks;

namespace Hashgraph
{
    public partial class Client
    {
        /// <summary>
        /// Retrieves the details regarding a file stored on the network.
        /// </summary>
        /// <param name="file">
        /// Address of the file to query.
        /// </param>
        /// <param name="configure">
        /// Optional callback method providing an opportunity to modify 
        /// the execution configuration for just this method call. 
        /// It is executed prior to submitting the request to the network.
        /// </param>
        /// <returns>
        /// The details of the network file, excluding content.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If required arguments are missing.</exception>
        /// <exception cref="InvalidOperationException">If required context configuration is missing.</exception>
        /// <exception cref="PrecheckException">If the gateway node create rejected the request upon submission.</exception>
        public async Task<FileInfo> GetFileInfoAsync(Address file, Action<IContext>? configure = null)
        {
            file = RequireInputParameter.File(file);
            await using var context = CreateChildContext(configure);
            var query = new Query
            {
                FileGetInfo = new FileGetInfoQuery
                {
                    Header = Transactions.CreateAskCostHeader(),
                    FileID = Protobuf.ToFileId(file)
                }
            };
            var response = await Transactions.ExecuteUnsignedAskRequestWithRetryAsync(context, query, getRequestMethod, getResponseHeader);
            long cost = (long)response.FileGetInfo.Header.Cost;
            if (cost > 0)
            {
                var transactionId = Transactions.GetOrCreateTransactionID(context);
                query.FileGetInfo.Header = await Transactions.CreateAndSignQueryHeaderAsync(context, cost, transactionId);
                response = await Transactions.ExecuteSignedRequestWithRetryAsync(context, query, getRequestMethod, getResponseHeader);
                ValidateResult.ResponseHeader(transactionId, getResponseHeader(response));
            }
            return Protobuf.FromFileInfo(response.FileGetInfo.FileInfo);

            static Func<Query, Task<Response>> getRequestMethod(Channel channel)
            {
                var client = new FileService.FileServiceClient(channel);
                return async (Query query) => (await client.getFileInfoAsync(query));
            }

            static ResponseHeader? getResponseHeader(Response response)
            {
                return response.FileGetInfo?.Header;
            }
        }
    }
}

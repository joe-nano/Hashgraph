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
        /// Retrieves detailed information regarding a Topic Instance.
        /// </summary>
        /// <param name="topic">
        /// The Hedera Network Address of the Topic instance to retrieve.
        /// </param>
        /// <param name="configure">
        /// Optional callback method providing an opportunity to modify 
        /// the execution configuration for just this method call. 
        /// It is executed prior to submitting the request to the network.
        /// </param>
        /// <returns>
        /// A detailed description of the contract instance.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If required arguments are missing.</exception>
        /// <exception cref="InvalidOperationException">If required context configuration is missing.</exception>
        /// <exception cref="PrecheckException">If the gateway node create rejected the request upon submission.</exception>
        public async Task<TopicInfo> GetTopicInfoAsync(Address topic, Action<IContext>? configure = null)
        {
            topic = RequireInputParameter.Topic(topic);
            await using var context = CreateChildContext(configure);
            var query = new Query
            {
                ConsensusGetTopicInfo = new ConsensusGetTopicInfoQuery
                {
                    Header = Transactions.CreateAskCostHeader(),
                    TopicID = Protobuf.ToTopicID(topic)
                }
            };
            var response = await Transactions.ExecuteUnsignedAskRequestWithRetryAsync(context, query, getRequestMethod, getResponseHeader);
            long cost = (long)response.ConsensusGetTopicInfo.Header.Cost;
            if (cost > 0)
            {
                var transactionId = Transactions.GetOrCreateTransactionID(context);
                query.ConsensusGetTopicInfo.Header = await Transactions.CreateAndSignQueryHeaderAsync(context, cost, transactionId);
                response = await Transactions.ExecuteSignedRequestWithRetryAsync(context, query, getRequestMethod, getResponseHeader);
                ValidateResult.ResponseHeader(transactionId, getResponseHeader(response));
            }
            return Protobuf.FromTopicInfo(response.ConsensusGetTopicInfo.TopicInfo);

            static Func<Query, Task<Response>> getRequestMethod(Channel channel)
            {
                var client = new ConsensusService.ConsensusServiceClient(channel);
                return async (Query query) => (await client.getTopicInfoAsync(query));
            }

            static ResponseHeader? getResponseHeader(Response response)
            {
                return response.ConsensusGetTopicInfo?.Header;
            }
        }
    }
}

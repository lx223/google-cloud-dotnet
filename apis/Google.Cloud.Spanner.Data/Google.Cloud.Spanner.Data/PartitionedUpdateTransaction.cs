﻿// Copyright 2018 Google LLC
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     https://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.V1;

namespace Google.Cloud.Spanner.Data
{
    /// <summary>
    /// Transaction used only to perform a partitioned update. This is never surfaced to the caller; it's like an ephemeral
    /// transaction but for partitioned updates.
    /// </summary>
    internal sealed class PartitionedUpdateTransaction : ISpannerTransaction, IDisposable
    {
        private readonly SpannerConnection _connection;
        private readonly Session _session;
        private readonly Transaction _wireTransaction;

        public PartitionedUpdateTransaction(SpannerConnection connection, Session session, Transaction wireTransaction)
        {
            _connection = connection;
            _session = session;
            _wireTransaction = wireTransaction;
        }

        public void Dispose() => _connection.ReleaseSession(_session, _connection.SpannerClient);

        public Task<int> ExecuteMutationsAsync(List<Mutation> mutations, CancellationToken cancellationToken, int timeoutSeconds) =>
            throw new NotSupportedException("A partitioned update transaction can only be used for generalized DML operations.");

        public Task<long> ExecuteDmlAsync(ExecuteSqlRequest request, CancellationToken cancellationToken, int timeoutSeconds)
        {
            GaxPreconditions.CheckNotNull(request, nameof(request));
            request.Transaction = new TransactionSelector { Id = _wireTransaction.Id };
            return _connection.ExecuteDmlAsync(_session, request, cancellationToken, timeoutSeconds, nameof(PartitionedUpdateTransaction));
        }

        public Task<ReliableStreamReader> ExecuteQueryAsync(ExecuteSqlRequest request,
            CancellationToken cancellationToken,
            int timeoutSeconds) =>
            throw new NotSupportedException("A partitioned update transaction can only be used for DML operations.");
    }
}

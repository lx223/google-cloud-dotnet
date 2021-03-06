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

using System.Collections.Generic;
using System.Linq;

namespace Google.Cloud.Spanner.V1
{
    public partial class SessionPool
    {
        /// <summary>
        /// A snapshot of statistics for a <see cref="SessionPool"/>.
        /// </summary>
        public sealed class Statistics
        {
            /// <summary>
            /// The total number of read-only sessions in the pool.
            /// </summary>
            public int TotalReadPoolCount => PerDatabaseStatistics.Sum(d => d.ReadPoolCount);
            /// <summary>
            /// The total number of read/write sessions in the pool.
            /// </summary>
            public int TotalReadWritePoolCount => PerDatabaseStatistics.Sum(d => d.ReadWritePoolCount);
            /// <summary>
            /// The total number of active sessions.
            /// </summary>
            public int TotalActiveSessionCount => PerDatabaseStatistics.Sum(d => d.ActiveSessionCount);

            /// <summary>
            /// The total number of session creation (or refresh, or transaction creation) requests in flight.
            /// </summary>
            public int TotalInFlightCreationCount => PerDatabaseStatistics.Sum(d => d.InFlightCreationCount);

            /// <summary>
            /// The total number of client calls awaiting sessions.
            /// </summary>
            public int TotalPendingAcquisitionCount => PerDatabaseStatistics.Sum(d => d.PendingAcquisitionCount);

            /// <summary>
            /// The statistics broken down by database.
            /// </summary>
            public IReadOnlyList<DatabaseStatistics> PerDatabaseStatistics { get; }

            internal Statistics(IReadOnlyList<DatabaseStatistics> perDatabaseStatistics) =>
                PerDatabaseStatistics = perDatabaseStatistics;
        }
    }
}

// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Cloud.ClientTesting;
using System;
using Xunit;

namespace Google.Cloud.BigQuery.V2.Snippets
{
    [SnippetOutputCollector]
    [Collection(nameof(BigQuerySnippetFixture))]
    public class BigQueryJobSnippets
    {
        private readonly BigQuerySnippetFixture _fixture;

        public BigQueryJobSnippets(BigQuerySnippetFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void ThrowOnAnyError()
        {
            string projectId = _fixture.ProjectId;

            // Snippet: ThrowOnAnyError
            BigQueryClient client = BigQueryClient.Create(projectId);

            string sql = $"This is a broken query";
            BigQueryJob query = client.CreateQueryJob(sql, parameters: null).PollUntilCompleted();
            try
            {
                // Usually this method is called in a chain. It returns the same job
                // if there are no errors.
                query = query.ThrowOnAnyError();
            }
            catch (GoogleApiException exception)
            {
                foreach (var error in exception.Error.Errors)
                {
                    Console.WriteLine($"Location: {error.Location}; Reason: {error.Reason}; Message: {error.Message}");
                }
            }
            // End sample

            Assert.Throws<GoogleApiException>(() => query.ThrowOnAnyError());
        }

        // See-also: BigQueryClient.PollJobUntilCompleted(string, *, *)
        // Member: PollUntilCompleted(*, *)
        // Member: PollUntilCompletedAsync(*, *, *)
        // See [BigQueryClient.PollJobUntilCompleted](ref) for an example using BigQueryClient.
        // End see-also

        // See-also: BigQueryClient.CancelJob(string, *)
        // Member: Cancel(*)
        // Member: CancelAsync(*, *)
        // See [BigQueryClient.CancelJob](ref) for an example using BigQueryClient.
        // End see-also

        // See-also: BigQueryClient.GetQueryResults(JobReference, *)
        // Member: GetQueryResults(*)
        // See [BigQueryClient.GetQueryResults](ref) for an example using BigQueryClient.
        // End see-also

        // See-also: BigQueryClient.GetQueryResultsAsync(JobReference, *, *)
        // Member: GetQueryResultsAsync(*, *)
        // See [BigQueryClient.GetQueryResultsAsync](ref) for an example using BigQueryClient.
        // End see-also
    }
}

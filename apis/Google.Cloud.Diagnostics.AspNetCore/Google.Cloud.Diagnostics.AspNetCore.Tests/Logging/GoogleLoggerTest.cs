﻿// Copyright 2016 Google Inc. All Rights Reserved.
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

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Api.Gax.Testing;
using Google.Cloud.Diagnostics.Common;
using Google.Cloud.Logging.V2;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Google.Cloud.Diagnostics.AspNetCore.Tests
{
    public class GoogleLoggerTest
    {
        private const string _logMessage = "a log message";
        private const string _logName = "log-name";
        private const string _baseLogName = "aspnetcore";
        private const string _projectId = "pid";
        private static readonly DateTime s_dateTime = DateTime.UtcNow;
        private static readonly Exception s_exception = new Exception("some message");
        private static readonly IClock s_clock = new FakeClock(s_dateTime);
        private static readonly LogTarget s_logTarget = LogTarget.ForProject(_projectId);

        /// <summary>
        /// Function to format a string and exception.  Used to test logging.
        /// </summary>
        private string Formatter(string message, Exception ex)
            => ex == null ? message : $"{message} - {ex.Message}";

        private GoogleLogger GetLogger(
            IConsumer<LogEntry> consumer, LogLevel logLevel = LogLevel.Information,
            Dictionary<string, string> labels = null, IServiceProvider serviceProvider = null,
            string logName = null)
        {
            LoggerOptions options = LoggerOptions.Create(logLevel, logName, labels, MonitoredResourceBuilder.GlobalResource);
            return new GoogleLogger(consumer, s_logTarget, options, _logName, s_clock, serviceProvider);
        }

        [Fact]
        public void BeginScope()
        {
            Predicate<IEnumerable<LogEntry>> matcher = (l) =>
                l.Single().JsonPayload.Fields["message"].StringValue == _logMessage &&
                l.Single().JsonPayload.Fields["scope"].StringValue == "scope => ";
            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, logLevel: LogLevel.Information);
            using (logger.BeginScope("scope"))
            {
                logger.Log(LogLevel.Error, 0, _logMessage, null, Formatter);
            }
            mockConsumer.VerifyAll();
        }

        [Fact]
        public void BeginScope_WithFormattedScope()
        {
            Predicate<IEnumerable<LogEntry>> matcher = l =>
            {
                var json = l.Single().JsonPayload.Fields;
                var parentScopes = json["parent_scopes"].ListValue.Values;
                var parentScope0 = parentScopes[0].StructValue.Fields;
                return json["message"].StringValue == _logMessage &&
                       json["scope"].StringValue == "scope 42, Baz => " &&
                       parentScopes.Count == 1 &&
                       parentScope0.Count == 3 &&
                       parentScope0["Foo"].StringValue == "42" &&
                       parentScope0["Bar"].StringValue == "Baz" &&
                       parentScope0["{OriginalFormat}"].StringValue == "scope {Foo}, {Bar}";
            };

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, logLevel: LogLevel.Information);
            using (logger.BeginScope("scope {Foo}, {Bar}", 42, "Baz"))
            {
                logger.LogError(_logMessage);
            }

            mockConsumer.VerifyAll();
        }

        [Fact]
        public void BeginScope_WithNestedFormattedScope()
        {
            Predicate<IEnumerable<LogEntry>> matcher = l =>
            {
                var json = l.Single().JsonPayload.Fields;
                var parentScopes = json["parent_scopes"].ListValue.Values;
                var scope0 = parentScopes[0].StructValue.Fields;
                var scope1 = parentScopes[1].StructValue.Fields;

                return json["message"].StringValue == _logMessage &&
                       json["scope"].StringValue == "first 42 => second Baz => " &&
                       parentScopes.Count == 2 &&
                       scope0.Count == 2 &&
                       scope0["{OriginalFormat}"].StringValue == "second {Bar}" &&
                       scope0["Bar"].StringValue == "Baz" &&
                       scope1.Count == 2 &&
                       scope1["{OriginalFormat}"].StringValue == "first {Foo}" &&
                       scope1["Foo"].StringValue == "42";
            };

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, logLevel: LogLevel.Information);
            using (logger.BeginScope("first {Foo}", 42))
            {
                using (logger.BeginScope("second {Bar}", "Baz"))
                {
                    logger.LogError(_logMessage);
                }
            }

            mockConsumer.VerifyAll();
        }

        [Fact]
        public void BeginScope_WithFormattedMessageAndScope()
        {
            var message = "a {things} message with stuff";
            var logParam = "log";

            Predicate<IEnumerable<LogEntry>> matcher = l =>
            {
                var json = l.Single().JsonPayload.Fields;
                var formatParams = json["format_parameters"].StructValue.Fields;
                var parentScopes = json["parent_scopes"].ListValue.Values;
                var parentScope0 = parentScopes[0].StructValue.Fields;
                return json["message"].StringValue == "a log message with stuff" &&
                       json["scope"].StringValue == "scope 42 => " &&
                       formatParams.Count == 2 &&
                       formatParams["things"].StringValue == logParam &&
                       formatParams["{OriginalFormat}"].StringValue == message &&
                       parentScopes.Count == 1 &&
                       parentScope0.Count == 2 &&
                       parentScope0["Foo"].StringValue == "42" &&
                       parentScope0["{OriginalFormat}"].StringValue == "scope {Foo}";
            };

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, logLevel: LogLevel.Information);
            using (logger.BeginScope("scope {Foo}", 42))
            {
                logger.LogError(message, logParam);
            }

            mockConsumer.VerifyAll();
        }

        [Fact]
        public void BeginScope_Nested()
        {
            Predicate<IEnumerable<LogEntry>> matcher = (l) =>
                l.Single().JsonPayload.Fields["message"].StringValue == _logMessage &&
                l.Single().JsonPayload.Fields["scope"].StringValue == "parent => child => ";
            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, LogLevel.Information);
            using (logger.BeginScope("parent"))
            {
                using (logger.BeginScope("child"))
                {
                    logger.Log(LogLevel.Error, 0, _logMessage, null, Formatter);
                }
            }
            mockConsumer.VerifyAll();
        }

        [Fact]
        public void IsEnabled()
        {
            var logger = GetLogger(new Mock<IConsumer<LogEntry>>().Object, LogLevel.Warning);
            Assert.False(logger.IsEnabled(LogLevel.Trace));
            Assert.False(logger.IsEnabled(LogLevel.Debug));
            Assert.False(logger.IsEnabled(LogLevel.Information));
            Assert.True(logger.IsEnabled(LogLevel.Warning));
            Assert.True(logger.IsEnabled(LogLevel.Error));
            Assert.True(logger.IsEnabled(LogLevel.Critical));
            Assert.True(logger.IsEnabled(LogLevel.None));
        }

        [Fact]
        public void Log_NullFormatter()
        {
            var logger = GetLogger(new Mock<IConsumer<LogEntry>>().Object);
            Assert.Throws<ArgumentNullException>(
                () => logger.Log(LogLevel.Information, 0, _logMessage, s_exception, null));
        }

        [Fact]
        public void Log_NotEnabled()
        {
            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            var logger = GetLogger(mockConsumer.Object);
            logger.Log(LogLevel.Debug, 0, _logMessage, s_exception, Formatter);
            mockConsumer.Verify(c => c.Receive(It.IsAny<IEnumerable<LogEntry>>()), Times.Never());
        }

        [Fact]
        public void Log_EmptyFormattedMessage()
        {
            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            var logger = GetLogger(mockConsumer.Object);
            logger.Log(LogLevel.Debug, 0, "", null, Formatter);
            mockConsumer.Verify(c => c.Receive(It.IsAny<IEnumerable<LogEntry>>()), Times.Never());
        }

        [Fact]
        public void Log()
        {
            var message = "a {things} message with stuff";
            var logStr = "log";

            var labels = new Dictionary<string, string> { { "some-key", "some-value" } };
            Predicate<IEnumerable<LogEntry>> matcher = logEntries =>
            {
                LogEntry entry = logEntries.Single();
                KeyValuePair<string, string> label = entry.Labels.Single();
                var json = entry.JsonPayload.Fields;
                return entry.LogName == new LogName(_projectId, _baseLogName).ToString() &&
                    entry.Severity == LogLevel.Error.ToLogSeverity() &&
                    string.IsNullOrWhiteSpace(entry.Trace) &&
                    entry.Timestamp.Equals(Timestamp.FromDateTime(s_dateTime)) &&
                    json["message"].StringValue == "a log message with stuff" &&
                    json["log_name"].StringValue == _logName &&
                    json["event_id"].StructValue.Fields["id"].NumberValue == 28 &&
                    json["format_parameters"].StructValue.Fields.Count == 2 &&
                    json["format_parameters"].StructValue.Fields["things"].StringValue == logStr &&
                    !json.ContainsKey("scope") &&
                    entry.Resource.Equals(MonitoredResourceBuilder.GlobalResource) &&
                    label.Key == "some-key" &&
                    label.Value == "some-value";
            };

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, LogLevel.Information, labels, null, _baseLogName);
            logger.LogError(28, s_exception, message, logStr);
            mockConsumer.VerifyAll();
        }

        [Fact]
        public void Log_EventName()
        {
            Predicate<IEnumerable<LogEntry>> matcher = logEntries =>
            {
                LogEntry entry = logEntries.Single();
                var json = entry.JsonPayload.Fields;
                return entry.LogName == new LogName(_projectId, _baseLogName).ToString() &&
                    json["message"].StringValue == _logMessage &&
                    json["event_id"].StructValue.Fields["id"].NumberValue == 11 &&
                    json["event_id"].StructValue.Fields["name"].StringValue == "some-event";
            };

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, LogLevel.Information, null, null, _baseLogName);
            var eventId = new EventId(11, "some-event");
            logger.LogError(eventId, s_exception, _logMessage);
            mockConsumer.VerifyAll();
        }

        [Fact]
        public void Log_NoFormatParams()
        {
            Predicate<IEnumerable<LogEntry>> matcher = logEntries =>
            {
                LogEntry entry = logEntries.Single();
                var json = entry.JsonPayload.Fields;
                return entry.LogName == new LogName(_projectId, _baseLogName).ToString() &&
                    json["message"].StringValue == _logMessage &&
                    !json.ContainsKey("format_parameters");
            };

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, LogLevel.Information, null, null, _baseLogName);
            logger.LogError(_logMessage);
            mockConsumer.VerifyAll();
        }

        [Fact]
        public void Log_Trace()
        {
            string traceId = "105445aa7843bc8bf206b12000100f00";
            string fullTraceName = TraceTarget.ForProject(_projectId).GetFullTraceName(traceId);

            Predicate<IEnumerable<LogEntry>> matcher = logEntries =>
            {
                LogEntry entry = logEntries.Single();
                return entry.LogName == new LogName(_projectId, _baseLogName).ToString() &&
                    entry.Trace == fullTraceName;
            };

            var tracerContext = TraceHeaderContext.Create(traceId, 81237123, null);
            HeaderDictionary dict = new HeaderDictionary();
            dict[TraceHeaderContext.TraceHeader] = tracerContext.ToString();

            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockAccessor = new Mock<IHttpContextAccessor>();
            var mockContext = new Mock<HttpContext>();
            var mockRequest = new Mock<HttpRequest>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IHttpContextAccessor))).Returns(mockAccessor.Object);
            mockAccessor.Setup(a => a.HttpContext).Returns(mockContext.Object);
            mockContext.Setup(c => c.Request).Returns(mockRequest.Object);
            mockRequest.Setup(r => r.Headers).Returns(dict);

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, LogLevel.Information, serviceProvider: mockServiceProvider.Object, logName: _baseLogName);
            logger.Log(LogLevel.Error, 0, _logMessage, s_exception, Formatter);
            mockConsumer.VerifyAll();
        }

        [Fact]
        public void Log_Labels()
        {
            Predicate<IEnumerable<LogEntry>> matcher = logEntries =>
            {
                LogEntry entry = logEntries.Single();
                var labelFoo = entry.Labels.First();
                var labelBar = entry.Labels.Skip(1).Single();

                return entry.LogName == new LogName(_projectId, _baseLogName).ToString() &&
                    labelFoo.Key == "Foo" &&
                    labelFoo.Value == "Hello" &&
                    labelBar.Key == "Bar" &&
                    labelBar.Value == "World";
            };

            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IEnumerable<ILogEntryLabelProvider>)))
                .Returns(new ILogEntryLabelProvider[] { new FooLogEntryLabelProvider(), new BarLogEntryLabelProvider() });

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, LogLevel.Information, serviceProvider: mockServiceProvider.Object, logName: _baseLogName);
            logger.LogInformation(_logMessage);
            mockConsumer.VerifyAll();
        }

        [Fact]
        public void Log_Labels_DefaultLabelsFirst()
        {
            var labels = new Dictionary<string, string> { { "some-key", "some-value" } };

            Predicate<IEnumerable<LogEntry>> matcher = logEntries =>
            {
                LogEntry entry = logEntries.Single();

                var defaultLabel = entry.Labels.First();
                var labelFoo = entry.Labels.Skip(1).First();
                var labelBar = entry.Labels.Skip(2).Single();

                return entry.LogName == new LogName(_projectId, _baseLogName).ToString() &&
                    defaultLabel.Key == "some-key" &&
                    defaultLabel.Value == "some-value" &&
                    labelFoo.Key == "Foo" &&
                    labelFoo.Value == "Hello" &&
                    labelBar.Key == "Bar" &&
                    labelBar.Value == "World";
            };

            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IEnumerable<ILogEntryLabelProvider>)))
                .Returns(new ILogEntryLabelProvider[] { new FooLogEntryLabelProvider(), new BarLogEntryLabelProvider() });

            var mockConsumer = new Mock<IConsumer<LogEntry>>();
            mockConsumer.Setup(c => c.Receive(Match.Create(matcher)));
            var logger = GetLogger(mockConsumer.Object, LogLevel.Information, labels: labels, serviceProvider: mockServiceProvider.Object, logName: _baseLogName);
            logger.LogInformation(_logMessage);
            mockConsumer.VerifyAll();
        }
    }

    internal class FooLogEntryLabelProvider : ILogEntryLabelProvider
    {
        public void Invoke(Dictionary<string, string> labels)
        {
            labels["Foo"] = "Hello";
        }
    }

    internal class BarLogEntryLabelProvider : ILogEntryLabelProvider
    {
        public void Invoke(Dictionary<string, string> labels)
        {
            labels["Bar"] = "World";
        }
    }
}

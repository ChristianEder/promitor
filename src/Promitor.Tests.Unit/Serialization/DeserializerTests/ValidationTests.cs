﻿using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Promitor.Core.Scraping.Configuration.Serialization;
using Promitor.Tests.Unit.Serialization.v1;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Promitor.Tests.Unit.Serialization.DeserializerTests
{
    public class ValidationTests
    {
        private readonly Mock<IErrorReporter> _errorReporter = new Mock<IErrorReporter>();
        private readonly TestDeserializer _deserializer = new TestDeserializer();
        
        [Fact]
        public void Deserialize_RequiredFieldMissing_ReportsError()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode("age: 20");

            // Act
            YamlAssert.ReportsErrorForProperty(
                _deserializer,
                node,
                "name");
        }
        
        [Fact]
        public void Deserialize_RequiredFieldProvided_DoesNotReportError()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode("name: Promitor");

            // Act
            _deserializer.Deserialize(node, _errorReporter.Object);

            // Assert
            _errorReporter.Verify(
                r => r.ReportError(node, It.Is<string>(message => message.Contains("name"))), Times.Never);
        }
        
        [Fact]
        public void Deserialize_OptionalFieldMissing_DoesNotReportError()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode("name: Promitor");

            // Act
            _deserializer.Deserialize(node, _errorReporter.Object);

            // Assert
            _errorReporter.Verify(
                r => r.ReportError(It.IsAny<YamlNode>(), It.Is<string>(message => message.Contains("age"))), Times.Never);
        }

        [Fact]
        public void Deserialize_UnknownFields_ReportsWarnings()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode(
@"city: Glasgow
country: Scotland");

            // Act
            _deserializer.Deserialize(node, _errorReporter.Object);

            // Assert
            var cityTagNode = node.Children.Single(c => c.Key.ToString() == "city").Key;
            var countryTagNode = node.Children.Single(c => c.Key.ToString() == "country").Key;
            
            _errorReporter.Verify(r => r.ReportWarning(cityTagNode, "Unknown field 'city'."));
            _errorReporter.Verify(r => r.ReportWarning(countryTagNode, "Unknown field 'country'."));
        }

        [Fact]
        public void Deserialize_EnumValueInvalid_ReportsError()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode("day: Sundag");
            var dayValueNode = node.Children.Single(c => c.Key.ToString() == "day").Value;

            // Act / Assert
            YamlAssert.ReportsError(
                _deserializer,
                node,
                dayValueNode,
                "'Sundag' is not a valid value for 'day'.");
        }

        [Fact]
        public void Deserialize_IntValueInvalid_ReportsError()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode("age: twenty");
            var dayValueNode = node.Children.Single(c => c.Key.ToString() == "age").Value;

            // Act / Assert
            YamlAssert.ReportsError(
                _deserializer,
                node,
                dayValueNode,
                $"'twenty' is not a valid value for 'age'. The value must be of type {typeof(int)}.");
        }

        [Fact]
        public void Deserialize_TimeSpanValueInvalid_ReportsError()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode("interval: twenty");
            var dayValueNode = node.Children.Single(c => c.Key.ToString() == "interval").Value;

            // Act / Assert
            YamlAssert.ReportsError(
                _deserializer,
                node,
                dayValueNode,
                "'twenty' is not a valid value for 'interval'. The value must be in the format 'hh:mm:ss'.");
        }

        [Fact]
        public void Deserialize_IgnoreField_DoesNotReportErrorIfFieldFound()
        {
            // Arrange
            var node = YamlUtils.CreateYamlNode("customField: 1234");

            // Act
            _deserializer.Deserialize(node, _errorReporter.Object);

            // Assert
            _errorReporter.Verify(
                r => r.ReportWarning(It.IsAny<YamlNode>(), It.Is<string>(s => s.Contains("customField"))), Times.Never);
        }

        [Fact]
        public void Deserialize_CalledMultipleTimes_DoesNotReusePreviousState()
        {
            // Since the deserializers are created once during startup, this test is to ensure
            // that if the same deserializer is used to deserialize multiple Yaml nodes, we don't
            // reuse the state from the previous time.

            // Arrange
            var node1 = YamlUtils.CreateYamlNode("name: Promitor");
            var node2 = YamlUtils.CreateYamlNode("age: 20");

            // Act
            _deserializer.Deserialize(node1, _errorReporter.Object);
            _deserializer.Deserialize(node2, _errorReporter.Object);

            // Assert
            _errorReporter.Verify(
                r => r.ReportError(It.IsAny<YamlNode>(), It.Is<string>(s => s.Contains("name"))));
        }

        private class TestConfigObject
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public DayOfWeek Day { get; set; }
            public TimeSpan Interval { get; set; }
        }

        private class TestDeserializer: Deserializer<TestConfigObject>
        {
            public TestDeserializer() : base(NullLogger.Instance)
            {
                MapRequired(t => t.Name);
                MapOptional(t => t.Age);
                MapOptional(t => t.Day);
                MapOptional(t => t.Interval);
                IgnoreField("customField");
            }
        }
    }
}
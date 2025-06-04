# IOS.Scheduler - Intelligent Outbound System Scheduler

## Overview

The IOS.Scheduler is a core service of the Intelligent Outbound System (IOS) that handles message processing, task scheduling, and device coordination through MQTT communication.

## Features

- **Message Processing**: Handles various types of messages from different system components
- **Task Scheduling**: Uses Quartz.NET for robust job scheduling
- **MQTT Integration**: Communicates with devices and other services via MQTT
- **Device Management**: Coordinates motion control, vision systems, coders, and sensors
- **Logging**: Comprehensive logging with Serilog
- **Health Monitoring**: Built-in health checks and heartbeat functionality

## Architecture

### Message Handlers

The system uses a factory pattern to route messages to appropriate handlers:

- **SystemMessageHandler**: Handles system-level commands and status updates
- **OutboundTaskHandler**: Processes outbound task requests and coordination
- **DeviceMessageHandler**: Manages general device communication
- **SensorMessageHandler**: Processes sensor data and events
- **MotionControlHandler**: Controls motion systems and robotics
- **VisionMessageHandler**: Handles vision system integration
- **CoderMessageHandler**: Manages coding/marking device operations
- **DefaultMessageHandler**: Fallback handler for unrecognized message types

### Core Services

- **SharedDataService**: Thread-safe data sharing between components
- **MessageHandlerFactory**: Routes messages to appropriate handlers
- **TaskScheduleService**: Manages scheduled tasks and jobs
- **OutboundTaskService**: Coordinates outbound operations

## Configuration

### MQTT Settings

```json
{
  "Mqtt": {
    "BrokerAddress": "localhost",
    "Port": 1883,
    "ClientId": "IOS.Scheduler",
    "Topics": {
      "Subscribe": [
        "system/+",
        "outbound/task/+",
        "device/+/+",
        "sensor/+",
        "motion/+",
        "vision/+",
        "coder/+"
      ]
    }
  }
}
```

### Scheduler Settings

```json
{
  "Scheduler": {
    "TaskTimeout": 300,
    "HeartbeatInterval": 30,
    "MaxConcurrentTasks": 10,
    "RetryAttempts": 3,
    "RetryDelay": 5000
  }
}
```

### Device Configuration

```json
{
  "Devices": {
    "MotionControl": {
      "Enabled": true,
      "Timeout": 30000,
      "RetryCount": 3
    },
    "VisionSystem": {
      "Enabled": true,
      "Timeout": 15000,
      "RetryCount": 2
    }
  }
}
```

## Message Topics

### Subscribed Topics

- `system/+` - System commands and status
- `outbound/task/+` - Task management
- `device/+/+` - Device communication
- `sensor/+` - Sensor data
- `motion/+` - Motion control
- `vision/+` - Vision system
- `coder/+` - Coding devices

### Published Topics

- `system/heartbeat/ios_scheduler` - Service heartbeat
- `system/status/ios_scheduler` - Service status
- `system/error/ios_scheduler` - Error notifications

## Running the Service

### Development

```bash
cd intelligentoutboundsystem/src/Services/IOS.Scheduler
dotnet run
```

### Production

```bash
dotnet publish -c Release
dotnet IOS.Scheduler.dll
```

### As Windows Service

```bash
sc create "IOS.Scheduler" binPath="C:\path\to\IOS.Scheduler.exe"
sc start "IOS.Scheduler"
```

## API Endpoints

- `GET /health` - Health check endpoint
- `GET /swagger` - API documentation (development only)

## Logging

The service uses Serilog for structured logging:

- Console output for development
- File logging with daily rotation
- Configurable log levels per namespace
- Machine name and thread ID enrichment

Log files are stored in the `logs/` directory with daily rotation and 7-day retention.

## Dependencies

- .NET 8.0
- Quartz.NET for scheduling
- MQTTnet for MQTT communication
- Serilog for logging
- Entity Framework Core for data access
- Swagger for API documentation

## Message Processing Flow

1. **Message Reception**: MQTT messages are received and queued
2. **Handler Selection**: MessageHandlerFactory selects appropriate handler
3. **Processing**: Handler processes the message based on topic and content
4. **Response**: Handler may publish response or update shared data
5. **Logging**: All activities are logged for monitoring and debugging

## Error Handling

- Automatic retry for failed operations
- Graceful degradation for device failures
- Comprehensive error logging
- Dead letter queue for unprocessable messages

## Monitoring

- Health check endpoints
- Heartbeat messages
- Performance metrics
- Structured logging for analysis

## Development Guidelines

1. **Adding New Handlers**: Implement `IMessageHandler` interface
2. **Configuration**: Add settings to `appsettings.json`
3. **Logging**: Use structured logging with appropriate log levels
4. **Testing**: Include unit tests for all handlers
5. **Documentation**: Update this README for new features

## Troubleshooting

### Common Issues

1. **MQTT Connection Failed**
   - Check broker address and port
   - Verify network connectivity
   - Check credentials if authentication is enabled

2. **Handler Not Found**
   - Verify handler registration in `Program.cs`
   - Check topic patterns in configuration

3. **Task Scheduling Issues**
   - Check Quartz configuration
   - Verify job registration
   - Review scheduler logs

### Log Analysis

Use the following log patterns to diagnose issues:

- `[ERR]` - Error conditions requiring attention
- `[WRN]` - Warning conditions that may need investigation
- `[INF]` - Informational messages about system operation
- `[DBG]` - Debug information for troubleshooting

## Contributing

1. Follow the existing code style and patterns
2. Add appropriate unit tests
3. Update documentation for new features
4. Use structured logging consistently
5. Handle errors gracefully with appropriate retry logic 
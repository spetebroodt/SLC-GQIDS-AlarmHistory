# History Alarms Data Source

## About

The **History Alarms Data Source** enables users to efficiently retrieve and analyze historical alarm data from their DataMiner System.

## Key Features

- **Time Range Filtering:** Specify a required start time (`From`) and an optional end time (`Until`) to focus on alarms within a particular period.
- **Search Functionality:** Filter alarms by a search term that matches the element name, parameter name, or value.
- **Efficient Paging:** Handles large alarm sets with server-side paging for optimal performance and scalability.

## Use Cases

- **Incident Analysis:** Quickly access all alarms that occurred during a specific incident window.
- **Root Cause Investigation:** Search for alarms related to specific elements, parameters, or values to support troubleshooting.
- **Historical Reporting:** Generate reports on alarm trends and patterns over custom time ranges.
- **Compliance Auditing:** Review alarm history for auditing and regulatory compliance purposes.

![Sample dashboard highlighting alarm filtering and search](./Images/dashboard.png)

## Input Arguments

- **From** (`DateTime`, required): Start of the time range for alarm retrieval.
- **Until** (`DateTime`, optional): End of the time range for alarm retrieval.
- **Search term** (`String`, optional): Text to search within element name, parameter name, or value.

## Output Columns

- **Severity**
- **Element**
- **Parameter**
- **Value**
- **Time**
- **Root ID**
- **ID**

## Prerequisites

- DataMiner System without GQI DxM (DxM support is currently under development)

## Technical Reference

Source code available at: [https://github.com/SkylineCommunications/SLC-GQIDS-AlarmHistory](https://github.com/SkylineCommunications/SLC-GQIDS-AlarmHistory)


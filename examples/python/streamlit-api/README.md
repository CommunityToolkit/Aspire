# Streamlit API Example

This is a simple Streamlit application that demonstrates how to run Streamlit apps in .NET Aspire using the Python Extensions.

## Prerequisites

- Python 3.12 or later
- .NET 8.0 or later
- Streamlit package

## Setup

1. Create a virtual environment:
   ```bash
   python -m venv .venv
   ```

2. Activate the virtual environment:
   - On Windows:
     ```bash
     .venv\Scripts\activate
     ```
   - On macOS/Linux:
     ```bash
     source .venv/bin/activate
     ```

3. Install dependencies:
   ```bash
   pip install streamlit
   ```

## Running the App

The app can be run as part of the Aspire AppHost project or standalone.

### Run with Aspire

Navigate to the AppHost project and run:
```bash
dotnet run
```

### Run Standalone

From this directory, run:
```bash
streamlit run app.py
```

## About the App

This is a basic Streamlit app that demonstrates:
- Simple text display
- Environment variable reading (PORT)
- Session state management
- Interactive buttons

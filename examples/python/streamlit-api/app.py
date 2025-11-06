import streamlit as st
import os

st.title("Hello, Aspire!")

st.write("This is a simple Streamlit app running in .NET Aspire.")

port = os.environ.get("PORT", "8501")
st.write(f"Running on port: {port}")

# Add a simple counter
if "counter" not in st.session_state:
    st.session_state.counter = 0

if st.button("Click me!"):
    st.session_state.counter += 1

st.write(f"Button clicked {st.session_state.counter} times")

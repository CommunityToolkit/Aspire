# Set the default image tag
ARG CONTAINER_IMAGE_TAG=17-ubuntu

# Start with a base image containing Java runtime
FROM mcr.microsoft.com/openjdk/jdk:${CONTAINER_IMAGE_TAG} as build

# Set the working directory
WORKDIR /app

# Set the default .jar file name
ARG JAR_NAME=spring-maven-0.0.1-SNAPSHOT.jar

# Copy the .jar file into the container at /app
COPY ./target/${JAR_NAME} app.jar

# Install wget and clean up the apt cache
RUN apt update && \
    apt upgrade -y && \
    apt install -y wget && \
    apt clean -y && \
    rm -rf /var/lib/apt/lists/*

# Set the default path for the OpenTelemetry Java agent
ARG AGENT_PATH=/agents

# Download the OpenTelemetry Java agent
RUN mkdir -p ${AGENT_PATH}
RUN wget -P ${AGENT_PATH} https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar
RUN chmod +x ${AGENT_PATH}/opentelemetry-javaagent.jar

# Set the default server port
ARG SERVER_PORT=8080
ENV SERVER_PORT=${SERVER_PORT}

# Make port available to the outside this container
EXPOSE ${SERVER_PORT}

# Run the jar file
ENTRYPOINT [ "java", "-Djava.security.egd=file:/dev/./urandom", "-jar", "/app/app.jar" ]

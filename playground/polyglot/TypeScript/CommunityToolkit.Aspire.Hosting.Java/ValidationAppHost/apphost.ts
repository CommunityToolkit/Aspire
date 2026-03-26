import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const javaProjectDirectory = '..\\..\\..\\..\\..\\examples\\java\\CommunityToolkit.Aspire.Hosting.Java.Spring.Maven';
const javaJarPath = 'target\\spring-maven-0.0.1-SNAPSHOT.jar';

const containerApp = await builder.addJavaContainerApp(
    'java-container',
    'docker.io/aliencube/aspire-spring-maven-sample',
    { imageTag: 'latest' });
await containerApp.withJvmArgs(['-Djava.awt.headless=true']);
await containerApp.withOtelAgent();
await containerApp.withExplicitStart();

await containerApp.entrypoint.set('java');
const _containerEntrypoint: string = await containerApp.entrypoint.get();
await containerApp.shellExecution.set(false);
const _containerShellExecution: boolean = await containerApp.shellExecution.get();
const _containerName: string = await containerApp.name.get();

const jarApp = await builder.addJavaAppWithJar(
    'java-jar',
    javaProjectDirectory,
    javaJarPath,
    { args: ['--spring.main.banner-mode=off'] });
await jarApp.withWrapperPath('mvnw.cmd');
await jarApp.withMavenBuild([]);
await jarApp.withJvmArgs(['-Duser.timezone=UTC']);
await jarApp.withOtelAgent();
await jarApp.withExplicitStart();

const _jarCommand: string = await jarApp.command.get();
const _jarWorkingDirectory: string = await jarApp.workingDirectory.get();
const _jarName: string = await jarApp.name.get();
const _jarPathBefore: string = await jarApp.jarPath.get();
await jarApp.jarPath.set(javaJarPath);
const _jarPathAfter: string = await jarApp.jarPath.get();

const mavenGoalApp = await builder.addJavaApp('java-maven-goal', javaProjectDirectory);
await mavenGoalApp.withWrapperPath('mvnw.cmd');
await mavenGoalApp.withMavenGoal('spring-boot:run', []);
await mavenGoalApp.withJvmArgs(['-Dfile.encoding=UTF-8']);
await mavenGoalApp.withOtelAgent();
await mavenGoalApp.withHttpEndpoint({ targetPort: 8080, env: 'SERVER_PORT' });
await mavenGoalApp.withHttpHealthCheck({ path: '/health' });

const _mavenGoalCommand: string = await mavenGoalApp.command.get();
const _mavenGoalWorkingDirectory: string = await mavenGoalApp.workingDirectory.get();
const _mavenGoalName: string = await mavenGoalApp.name.get();

const gradleBuildApp = await builder.addJavaApp('java-gradle-build', javaProjectDirectory);
await gradleBuildApp.withWrapperPath('gradlew.bat');
await gradleBuildApp.withGradleBuild(['--parallel']);
await gradleBuildApp.withExplicitStart();

const gradleTaskApp = await builder.addJavaApp('java-gradle-task', javaProjectDirectory);
await gradleTaskApp.withWrapperPath('gradlew.bat');
await gradleTaskApp.withGradleTask('bootRun', ['--args=--spring.main.banner-mode=off']);
await gradleTaskApp.withExplicitStart();

await builder.build().run();

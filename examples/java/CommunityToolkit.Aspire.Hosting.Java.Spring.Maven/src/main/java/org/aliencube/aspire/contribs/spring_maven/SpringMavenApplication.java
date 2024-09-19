package org.aliencube.aspire.contribs.spring_maven;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.ComponentScan;

@SpringBootApplication
@ComponentScan({"org.aliencube.aspire.contribs.spring_maven.controllers"})
public class SpringMavenApplication {
    public static void main(String[] args) {
		SpringApplication.run(SpringMavenApplication.class, args);
	}

}

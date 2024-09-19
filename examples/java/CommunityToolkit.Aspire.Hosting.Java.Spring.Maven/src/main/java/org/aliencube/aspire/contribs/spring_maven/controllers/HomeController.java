package org.aliencube.aspire.contribs.spring_maven.controllers;

import java.util.HashMap;
import java.util.Map;

import org.springframework.http.MediaType;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
public class HomeController {

    @GetMapping(value = "/", produces = MediaType.APPLICATION_JSON_VALUE)
    public Map<String, String> get() {
        Map<String, String> response = new HashMap<>();
        response.put("weatherforecast", "/api/weatherforecast");

        return response;
    }

}

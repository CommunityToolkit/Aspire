package org.aliencube.aspire.contribs.spring_maven.controllers;

import java.time.LocalDate;
import java.util.HashMap;
import java.util.Map;
import java.util.Random;
import java.util.stream.Collectors;
import java.util.stream.IntStream;

import org.aliencube.aspire.contribs.spring_maven.models.WeatherForecast;

import org.springframework.http.MediaType;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RequestMapping(value = "/api")
@RestController
public class WeatherController {

    private final String[] summaries = { "얼어붙는", "활기찬", "쌀쌀한", "시원한", "온화한", "따뜻한", "포근한", "더운", "무더운", "타는듯한" };

    @GetMapping(value = "/weatherforecast", produces = MediaType.APPLICATION_JSON_VALUE)
    public WeatherForecast[] getWeatherForecast() {
        Random random = new Random();
        return IntStream.range(1, 6)
                .mapToObj(i -> new WeatherForecast(
                        LocalDate.now().plusDays(i),
                        random.nextInt(75) - 20,
                        summaries[random.nextInt(summaries.length)]))
                .toArray(WeatherForecast[]::new);
    }

}

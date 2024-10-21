import { useState, useEffect } from "react";
import "./App.css";

type Forecast = {
    date: string;
    temperatureC: number;
    temperatureF: number;
    summary: string;
};

function App() {
    const [forecasts, setForecasts] = useState<Forecast[]>([]);

    useEffect(() => {
        fetch("/api/weather")
            .then((response) => response.json())
            .then((data) => setForecasts(data));
    }, []);

    return (
        <>
            {forecasts.length === 0 && <div>Loading...</div>}
            {forecasts.length > 0 && (
                <table className="table">
                    <thead>
                        <tr>
                            <th>Date</th>
                            <th>Temp. (C)</th>
                            <th>Temp. (F)</th>
                            <th>Summary</th>
                        </tr>
                    </thead>
                    <tbody>
                        {forecasts.map((f) => (
                            <tr>
                                <td>{f.date}</td>
                                <td>{f.temperatureC}</td>
                                <td>{f.temperatureF}</td>
                                <td>{f.summary}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </>
    );
}

export default App;

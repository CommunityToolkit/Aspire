import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import "./bootstrap.css";

createRoot(document.getElementById("root")!).render(
    <StrictMode>
        <div className="page">
            <div className="sidebar"></div>

            <main>
                <div className="top-row px-4">
                    <a
                        href="https://github.com/CommunityToolkit/Aspire/"
                        target="_blank"
                    >
                        About
                    </a>
                </div>

                <article className="content px-4">
                    <App />
                </article>
            </main>
        </div>
    </StrictMode>
);

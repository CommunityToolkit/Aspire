export function setupUmamiAnalytics(element: HTMLElement) {
  const umamiEndpoint = import.meta.env.VITE_UMAMI_ENDPOINT;
  const umamiWebsiteId = import.meta.env.VITE_UMAMI_WEBSITE_ID;

  const scriptPath = "script.js";

  const scriptElement = document.createElement("script");
  scriptElement.defer = true;
  scriptElement.src = `${umamiEndpoint}/${scriptPath}`;
  scriptElement.dataset.websiteId = umamiWebsiteId;

  element.appendChild(scriptElement);
}

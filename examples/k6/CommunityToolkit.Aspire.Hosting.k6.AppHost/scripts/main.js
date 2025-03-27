import http from "k6/http";
import { sleep } from "k6";

export default function () {
    http.get(`${__ENV.services__apiservice__http__0}/hello`);

    sleep(1);
}

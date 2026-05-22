package app

import (
	"fmt"
	"log"
	"net/http"
	"os"
)

// Start runs the validation HTTP server.
func Start(defaultMode string) {
	mode := defaultMode
	args := os.Args[1:]
	for i := 0; i < len(args)-1; i++ {
		if args[i] == "--mode" {
			mode = args[i+1]
			break
		}
	}

	port := os.Getenv("PORT")
	if port == "" {
		switch mode {
		case "root":
			port = "8081"
		case "cmd-server":
			port = "8082"
		default:
			port = "8080"
		}
	}

	mux := http.NewServeMux()
	mux.HandleFunc("/", func(w http.ResponseWriter, _ *http.Request) {
		_, _ = fmt.Fprintf(w, "hello from %s", mode)
	})

	log.Printf("starting %s on %s", mode, port)
	if err := http.ListenAndServe(":"+port, mux); err != nil {
		log.Fatal(err)
	}
}

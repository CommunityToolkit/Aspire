variable "message" {
  type = string
}

resource "terraform_data" "sample" {
  input = var.message
}

output "message" {
  value = terraform_data.sample.output
}

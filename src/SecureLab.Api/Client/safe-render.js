// Descriptions and comments are plain text, including strings that look like HTML.
export function renderPlainText(element, value) {
  element.textContent = value ?? "";
}

// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}
export function write(message) {
    console.log(message);
}

export function focusFirstInvalidField () {

    // Find the first validation message element
    const firstInvalidElement = document.querySelector('.invalid');

    // log the name of the first invalid element
    if (firstInvalidElement) {
        console.log("First invalid element found:", firstInvalidElement.id);
    } else {
        console.log("No invalid elements found.");
    }

    if (firstInvalidElement) {

        firstInvalidElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        const inputId = firstInvalidElement.id;

        // Find the associated input element to focus
        if (inputId) {
            const inputElement = document.getElementById(inputId);
            if (inputElement) {

                console.log("Focusing on input element:", inputElement.id);

                inputElement.focus();
                return;
            }
        }
    }
};
window.focusFirstInvalidField = function () {

    // Find the first validation message element
    const firstInvalidElement = document.querySelector('.invalid');

    // log the name of the first invalid element
    if (firstInvalidElement) {
        console.log("First invalid element found:", firstInvalidElement.id);
    } else {
        console.log("No invalid elements found.");
    }

    if (firstInvalidElement) {

        firstInvalidElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        const inputId = firstInvalidElement.id;

        // Find the associated input element to focus
        if (inputId) {
            const inputElement = document.getElementById(inputId);
            if (inputElement) {

                console.log("Focusing on input element:", inputElement.id);

                inputElement.focus();
                return;
            }
        }
    }
};
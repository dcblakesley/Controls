window.focusFirstInvalidField = function () {

    // Find the first validation message element
    const firstInvalidElement = document.querySelector('.invalid');

    if (firstInvalidElement) {

        firstInvalidElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        const inputId = firstInvalidElement.id;

        // Find the associated input element to focus
        if (inputId) {
            const inputElement = document.getElementById(inputId);
            if (inputElement) {
                inputElement.focus();
                // Select the text in the input field
                if (inputElement.tagName === 'INPUT' || inputElement.tagName === 'TEXTAREA') {
                    inputElement.select();
                }
                return;
            }
        }
    }
};

window.log = function (text) {
    console.log(text);
};
window.logError = function (text) {
    var css = 'background: red';
    console.log('%c' + text, css);
};
window.logWarn = function (text) {
    var css = 'background: orange';
    console.log('%c' + text, css);
};
window.logInfo = function (text) {
    var css = 'background: cyan';
    console.log('%c' + text, css);
};
﻿const vs = new aspnetValidation.ValidationService();

vs.addProvider("requiredif", function (value, element, params) {
    const otherElement = document.getElementsByName(params.property)[0];
    if (otherElement && otherElement.value === params.value) {
        const isValid = !!value;
        if (isValid && !!element.dataset.valRequiredifSkip) {
            // trigger other property validation if this one valid
            otherElement.dataset.valRequiredifSkip = true;
            vs.isFieldValid(otherElement, true);
        }

        element.dataset.valRequiredifSkip = false;
        return isValid;
    }

    return true;
});

vs.bootstrap();

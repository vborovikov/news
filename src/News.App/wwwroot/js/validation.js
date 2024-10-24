const vs = new aspnetValidation.ValidationService();

vs.addProvider("requiredif", function (value, element, params) {
    if (params.others !== undefined) {
        const isValid = !!value;
        const revalidateOthers = element.dataset.valRequiredifReval;
        let validated = false;

        const others = new Map(Object.entries(JSON.parse(params.others)));
        for (const [otherName, otherValue] of others) {
            const otherElement = document.getElementsByName(otherName)[0];
            if (otherElement && otherElement.value === otherValue) {
                if (isValid && revalidateOthers) {
                    // trigger other field validation if this one valid
                    otherElement.dataset.valRequiredifReval = false;
                    vs.isFieldValid(otherElement, true);
                }
                validated = true;
            }
        }

        if (validated) {
            element.dataset.valRequiredifReval = true;
            return isValid;
        }
    }

    return true;
});

vs.bootstrap();

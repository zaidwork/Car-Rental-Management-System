window.vehicleDiagramInterop = {
    getRelativeClick: function (elementId, clientX, clientY) {
        var el = document.getElementById(elementId);
        if (!el) return { x: 50.0, y: 50.0 };
        // If el has an img sibling, child or is an img, use the img's exact bounding box
        var img = null;
        if (el.tagName && el.tagName.toUpperCase() === 'IMG') {
            img = el;
        } else if (el.querySelector('img')) {
            img = el.querySelector('img');
        } else if (el.parentElement && el.parentElement.querySelector('img')) {
            img = el.parentElement.querySelector('img');
        }
        var target = img || el;
        var rect = target.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return { x: 50.0, y: 50.0 };
        var x = ((clientX - rect.left) / rect.width) * 100.0;
        var y = ((clientY - rect.top) / rect.height) * 100.0;
        x = Math.max(0.5, Math.min(99.5, x));
        y = Math.max(0.5, Math.min(99.5, y));
        return { x: parseFloat(x.toFixed(2)), y: parseFloat(y.toFixed(2)) };
    }
};


'use strict';
window.IllustratorConversionScript=`#target illustrator
(function () {
    var source = File.openDialog("Choose the SVG exported from Toolkit", "*.svg");
    if (!source) return;
    var destination = File.saveDialog("Save native Illustrator artwork", "*.ai");
    if (!destination) return;
    if (!/\\.ai$/i.test(destination.name)) destination = new File(destination.fsName + ".ai");
    var document = null;
    try {
        document = app.open(source);
        var options = new IllustratorSaveOptions();
        options.pdfCompatible = true;
        document.saveAs(destination, options);
        document.close(SaveOptions.DONOTSAVECHANGES);
        document = null;
        alert("Saved native AI: " + destination.fsName);
    } catch (error) {
        if (document) { try { document.close(SaveOptions.DONOTSAVECHANGES); } catch (ignored) {} }
        alert("AI export failed: " + error.message);
    }
})();
`;

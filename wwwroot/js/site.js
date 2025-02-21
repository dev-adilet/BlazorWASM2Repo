// wwwroot/js/site.js
window.downloadFile = (filename, content) => {
    const element = document.createElement('a');
    // Set the href to a data URI containing the file's content in JSON format.
    element.setAttribute('href', 'data:text/json;charset=utf-8,' + encodeURIComponent(content));
    element.setAttribute('download', filename);
    // Append the element to the document body to initiate the download.
    document.body.appendChild(element);
    element.click();
    // Remove the element after download is initiated.
    document.body.removeChild(element);
};

(function () {
  "use strict";

  var status = document.getElementById("demo-status");
  if (!status) return;

  var params = new URLSearchParams(window.location.search);
  if (params.get("sent") === "1") {
    status.hidden = false;
    status.textContent = "Request received. A reply goes to the email you sent.";
  } else if (params.get("error") === "1") {
    status.hidden = false;
    status.textContent = "That request did not send. Check the name and email, then try again.";
  }
})();

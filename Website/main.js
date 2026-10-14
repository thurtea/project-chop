(function () {
  "use strict";

  var nav = document.querySelector(".nav");
  var toggle = document.querySelector(".nav-toggle");
  var menu = document.getElementById("site-nav");
  var links = menu ? menu.querySelectorAll('a[href^="#"]') : [];
  var sections = [];

  function setOpen(open) {
    if (!nav || !toggle) return;
    nav.classList.toggle("is-open", open);
    toggle.setAttribute("aria-expanded", open ? "true" : "false");
    toggle.setAttribute("aria-label", open ? "Close menu" : "Open menu");
  }

  if (toggle) {
    toggle.addEventListener("click", function () {
      setOpen(!nav.classList.contains("is-open"));
    });
  }

  Array.prototype.forEach.call(links, function (link) {
    var id = link.getAttribute("href").slice(1);
    var section = document.getElementById(id);
    if (section) sections.push({ link: link, section: section });

    link.addEventListener("click", function () {
      setOpen(false);
    });
  });

  function updateCurrent() {
    var current = null;
    var marker = window.scrollY + 120;

    for (var i = 0; i < sections.length; i += 1) {
      if (sections[i].section.offsetTop <= marker) {
        current = sections[i].link;
      }
    }

    for (var j = 0; j < sections.length; j += 1) {
      if (sections[j].link === current) {
        sections[j].link.setAttribute("aria-current", "true");
      } else {
        sections[j].link.removeAttribute("aria-current");
      }
    }
  }

  if (sections.length) {
    updateCurrent();
    window.addEventListener("scroll", updateCurrent, { passive: true });
  }

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") setOpen(false);
  });
})();

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

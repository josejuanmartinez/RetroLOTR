document.getElementById('year').textContent = new Date().getFullYear();

(function () {
  var slides = document.querySelectorAll('.hero-slide');
  if (slides.length < 2 || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  var current = 0;
  setInterval(function () {
    slides[current].classList.remove('active');
    current = (current + 1) % slides.length;
    slides[current].classList.add('active');
  }, 4500);
})();

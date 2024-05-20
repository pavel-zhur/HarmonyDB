export function MainLayoutRendered() {
    App.initCore();
    App.initAfterLoad();
}

export function InitSecondToTop() {

	// Elements
	const toTopElement = document.createElement('button'),
		toTopElementIcon = document.createElement('i'),
		toTopButtonContainer = document.querySelector('.btn-to-top2'),
		toTopButtonColorClass = 'btn-secondary',
		toTopButtonIconClass = 'ph-arrow-up',
		scrollableContainer = document.querySelector('#my-secondary-sidebar-content'),
		scrollableDistance = 250,
		footerContainer = document.querySelector('.navbar-footer');


	// Append only if container exists
	if (scrollableContainer) {


		if (scrollableContainer.getAttribute('execOnce') == "yes") return;
		scrollableContainer.setAttribute('execOnce', "yes");

		// Create button
		toTopElement.classList.add('btn', toTopButtonColorClass, 'btn-icon', 'rounded-pill');
		toTopElement.setAttribute('type', 'button');
		toTopButtonContainer.appendChild(toTopElement);
		toTopElementIcon.classList.add(toTopButtonIconClass);
		toTopElement.appendChild(toTopElementIcon);

		// Show and hide on scroll
		const to_top_button = document.querySelector('.btn-to-top2'),
			add_class_on_scroll = () => to_top_button.classList.add('btn-to-top2-visible'),
			remove_class_on_scroll = () => to_top_button.classList.remove('btn-to-top2-visible');

		scrollableContainer.addEventListener('scroll', function () {
			const scrollpos = scrollableContainer.scrollTop;
			scrollpos >= scrollableDistance ? add_class_on_scroll() : remove_class_on_scroll();
			if (footerContainer) {
				if (this.scrollHeight - this.scrollTop - this.clientHeight <= footerContainer.clientHeight) {
					to_top_button.style.bottom = footerContainer.clientHeight + 20 + 'px';
				}
				else {
					to_top_button.removeAttribute('style');
				}
			}
		});

		// Scroll to top on click
		document.querySelector('.btn-to-top2 .btn').addEventListener('click', function () {
			scrollableContainer.scrollTo(0, 0);
		});
	}
}
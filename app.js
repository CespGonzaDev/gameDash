// Elementos del DOM
const searchInput = document.getElementById('searchInput');
const sortSelect = document.getElementById('sortSelect');
const gamesGrid = document.getElementById('gamesGrid');
const resultsCount = document.getElementById('resultsCount');
const emptyState = document.getElementById('emptyState');
const errorState = document.getElementById('errorState');
const modal = document.getElementById('modal');
const modalBody = document.getElementById('modalBody');
const closeModalBtn = document.getElementById('closeModal');
const modalOverlay = modal.querySelector('.modal-overlay');
const themeToggle = document.getElementById('themeToggle');
const themeIcon = document.querySelector('.theme-icon');


// Filtros
const filterFree = document.getElementById('filterFree');
const filterUnder2h = document.getElementById('filterUnder2h');
const filterUnder5h = document.getElementById('filterUnder5h');
const filterUnder10h = document.getElementById('filterUnder10h');

// Estado
let allGames = [];
let filteredGames = [];


//Gestion de tema
function initTheme() {
  const savedTheme = localStorage.getItem('theme') || 'dark';
  if (savedTheme === 'light') {
    document.body.classList.add('light-theme');
    themeIcon.textContent = '☀️';
  } else {
    themeIcon.textContent = '🌙';
  }
}

function toggleTheme() {
  document.body.classList.toggle('light-theme');
  const isLight = document.body.classList.contains('light-theme');
  themeIcon.textContent = isLight ? '☀️' : '🌙';
  localStorage.setItem('theme', isLight ? 'light' : 'dark');
}

themeToggle.addEventListener('click', toggleTheme);

// Inicializar tema al cargar
initTheme();

// Detectar plataforma desde la URL
function detectPlatform(url) {
  if (!url) return 'PC';
  const lowerUrl = url.toLowerCase();
  if (lowerUrl.includes('steam') || lowerUrl.includes('pc')) return '🖥️ PC';
  if (lowerUrl.includes('playstation') || lowerUrl.includes('ps')) return '🎮 PlayStation';
  if (lowerUrl.includes('xbox')) return '🎮 Xbox';
  if (lowerUrl.includes('switch')) return '🎮 Switch';
  return '🖥️ PC';
}

// Normalizar datos del JSON
function normalizeGame(rawGame) {
  const stores = rawGame.Tiendas || {};
  const offers = Object.entries(stores)
    .map(([key, store]) => ({
      storeKey: key,
      storeName: store.StoreName,
      url: store.Url || null,
      price: typeof store.PriceNumber === 'number' ? store.PriceNumber : null,
      priceRaw: store.PriceRaw || null
    }))
    .filter(offer => offer.price !== null);

  const bestOffer = offers.length > 0 
    ? offers.sort((a, b) => a.price - b.price)[0] 
    : null;

  // Extraer precio original del PriceRaw
  let regularPrice = null;
  if (bestOffer && offers.length > 0) {
    const raw = offers.find(o => o.priceRaw)?.priceRaw || '';
    const numbers = (raw.match(/[\d.]+/g) || [])
      .map(Number)
      .sort((a, b) => b - a);
    regularPrice = numbers.find(n => n > bestOffer.price) ?? null;
  }

  const discountPercent = (bestOffer && regularPrice)
    ? Math.round((1 - bestOffer.price / regularPrice) * 100)
    : null;

  const platform = detectPlatform(rawGame.FuenteInicialUrl);
  const isFree = offers.some(o => o.price === 0);

  return {
    id: rawGame.Nombre,
    title: rawGame.Nombre,
    coverUrl: rawGame.ImagenUrl || '',
    rating: rawGame.Analisis?.Calificacion ?? null,
    avgHours: rawGame.Analisis?.HorasPromedio ?? null,
    reviewCount: rawGame.Analisis?.CantidadResenas ?? null,
    sourceUrl: rawGame.FuenteInicialUrl || null,
    platform,
    offers,
    bestOffer,
    regularPrice,
    discountPercent,
    isFree
  };
}

// Renderizar juegos
function renderGames(games) {
  if (games.length === 0) {
    gamesGrid.innerHTML = '';
    emptyState.style.display = 'block';
    errorState.style.display = 'none';
    resultsCount.textContent = '';
    return;
  }

  emptyState.style.display = 'none';
  errorState.style.display = 'none';

  gamesGrid.innerHTML = games.map(game => `
    <div class="game-card" data-game-id="${game.id}">
      <img 
        class="game-card-image" 
        src="${game.coverUrl}" 
        alt="${game.title}"
        onerror="this.style.opacity='0.3'"
      >
      <div class="game-card-body">
        <h3 class="game-card-title">${game.title}</h3>
        <div class="game-card-platform">${game.platform}</div>
        <div class="game-card-pricing">
          <div class="price-row">
            <span class="price-current">
              ${game.bestOffer ? (game.isFree ? 'GRATIS' : '$' + game.bestOffer.price.toFixed(2)) : 'N/D'}
            </span>
            ${game.regularPrice ? `<span class="price-original">$${game.regularPrice.toFixed(2)}</span>` : ''}
          </div>
          <div class="game-card-badges">
            ${game.discountPercent ? `<div class="discount-badge">-${game.discountPercent}%</div>` : ''}
            ${game.rating ? `<div class="rating-badge">⭐${game.rating}</div>` : ''}
          </div>
        </div>
      </div>
    </div>
  `).join('');

  resultsCount.textContent = `Mostrando ${games.length} juego${games.length !== 1 ? 's' : ''}`;
}

// Filtrar juegos
function filterGames() {
  const searchTerm = searchInput.value.toLowerCase().trim();
  const showFree = filterFree.checked;
  const showUnder2h = filterUnder2h.checked;
  const showUnder5h = filterUnder5h.checked;
  const showUnder10h = filterUnder10h.checked;
  
  filteredGames = allGames.filter(game => {
    // Búsqueda por nombre
    const matchesSearch = game.title.toLowerCase().includes(searchTerm);
    
    // Filtro de gratis
    const matchesFree = !showFree || game.isFree;
    
    // Filtros de duración
    let matchesDuration = true;
    if (showUnder2h || showUnder5h || showUnder10h) {
      const hours = game.avgHours ?? Infinity;
      matchesDuration = 
        (showUnder2h && hours < 2) ||
        (showUnder5h && hours < 5) ||
        (showUnder10h && hours < 10);
    }
    
    return matchesSearch && matchesFree && matchesDuration;
  });
  
  sortGames();
}

// Ordenar juegos
function sortGames() {
  const sortBy = sortSelect.value;
  
  switch (sortBy) {
    case 'name':
      filteredGames.sort((a, b) => a.title.localeCompare(b.title));
      break;
    case 'priceAsc':
      filteredGames.sort((a, b) => 
        (a.bestOffer?.price ?? Infinity) - (b.bestOffer?.price ?? Infinity)
      );
      break;
    case 'priceDesc':
      filteredGames.sort((a, b) => 
        (b.bestOffer?.price ?? 0) - (a.bestOffer?.price ?? 0)
      );
      break;
    case 'ratingDesc':
      filteredGames.sort((a, b) => (b.rating ?? 0) - (a.rating ?? 0));
      break;
    case 'discountDesc':
      filteredGames.sort((a, b) => 
        (b.discountPercent ?? 0) - (a.discountPercent ?? 0)
      );
      break;
    case 'mostReviewed':
      filteredGames.sort((a, b) => (b.reviewCount ?? 0) - (a.reviewCount ?? 0));
      break;
  }
  
  renderGames(filteredGames);
}

// Cargar juegos desde Firebase
async function loadGames() {
  resultsCount.textContent = 'Cargando...';
  gamesGrid.innerHTML = '';
  emptyState.style.display = 'none';
  errorState.style.display = 'none';
  
  try {
    const firebaseUrl = 'https://resultadosscrapping-default-rtdb.firebaseio.com/resultados.json';
    const response = await fetch(firebaseUrl);
    
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    
    const rawData = await response.json();
    allGames = rawData.map(normalizeGame);
    filteredGames = [...allGames];
    sortGames();
  } catch (error) {
    console.error('Error cargando los juegos:', error);
    gamesGrid.innerHTML = '';
    emptyState.style.display = 'none';
    errorState.style.display = 'block';
    resultsCount.textContent = '';
  }
}

// Mostrar modal
function showModal(game) {
  modalBody.innerHTML = `
    <img class="modal-image" src="${game.coverUrl}" alt="${game.title}" onerror="this.style.display='none'">
    <div class="modal-details">
      <h2>${game.title}</h2>
      <p><strong>Plataforma:</strong> ${game.platform}</p>
      <p><strong>Calificación:</strong> ${game.rating ? '⭐ ' + game.rating : '—'}</p>
      <p><strong>Horas Promedio:</strong> ${game.avgHours ?? '—'}</p>
      <p><strong>Cantidad de Reseñas:</strong> ${game.reviewCount ? game.reviewCount.toLocaleString() : '—'}</p>
      <p><strong>Mejor Precio:</strong> ${game.bestOffer ? (game.isFree ? 'GRATIS' : '$' + game.bestOffer.price.toFixed(2)) : '—'}</p>
      <p><strong>Precio Original:</strong> ${game.regularPrice ? '$' + game.regularPrice.toFixed(2) : '—'}</p>
      ${game.discountPercent ? `<p><strong>Descuento:</strong> <span style="color: var(--success)">-${game.discountPercent}%</span></p>` : ''}
      
      <h3>Ofertas Disponibles</h3>
      <table class="offers-table">
        <thead>
          <tr>
            <th>Tienda</th>
            <th>Precio</th>
            <th>Descuento</th>
            <th>Acción</th>
          </tr>
        </thead>
        <tbody>
          ${game.offers.map(offer => `
            <tr>
              <td>${offer.storeName}</td>
              <td>${offer.price === 0 ? 'GRATIS' : (offer.price ? '$' + offer.price.toFixed(2) : '—')}</td>
              <td>${game.regularPrice && offer.price 
                ? Math.round((1 - offer.price / game.regularPrice) * 100) + '%' 
                : '—'}</td>
              <td>${offer.url ? `<a href="${offer.url}" target="_blank" rel="noopener">Visitar ➜</a>` : ''}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
      
      ${game.sourceUrl ? `<a href="${game.sourceUrl}" target="_blank" rel="noopener" style="color: var(--accent);">Ver en Steam ➜</a>` : ''}
    </div>
  `;
  
  modal.style.display = 'flex';
}

// Cerrar modal
function closeModal() {
  modal.style.display = 'none';
}

// Event listeners
searchInput.addEventListener('input', filterGames);
sortSelect.addEventListener('change', sortGames);
filterFree.addEventListener('change', filterGames);
filterUnder2h.addEventListener('change', filterGames);
filterUnder5h.addEventListener('change', filterGames);
filterUnder10h.addEventListener('change', filterGames);

gamesGrid.addEventListener('click', (e) => {
  const card = e.target.closest('.game-card');
  if (!card) return;
  
  const gameId = card.dataset.gameId;
  const game = filteredGames.find(g => g.id === gameId);
  
  if (game) {
    showModal(game);
  }
});

closeModalBtn.addEventListener('click', closeModal);
modalOverlay.addEventListener('click', closeModal);

document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && modal.style.display === 'flex') {
    closeModal();
  }
});

// Inicializar
loadGames();

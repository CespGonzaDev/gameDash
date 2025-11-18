
<p align="center">
<img src="https://github.com/user-attachments/assets/d7e7e6e0-e6f2-4607-a02a-97cc914e034f" width="350"/>
</p>


<h4 align="center">GameDash — Motor de Web Scraping Multicore + Página Web de Ofertas de Videojuegos</h4>
<h5 align="center">GameDash es un proyecto académico desarrollado como parte del III Proyecto de Programación Multicore.</h5>

El sistema combina **web scraping en paralelo**, **procesamiento multicore**,
y una **interfaz web moderna** para mostrarinformación consolidada sobre 
videojuegos, como:

- Precios en distintas tiendas digitales
- Descuentos
- Horas de juego estimadas
- Calificaciones (usuarios / críticas)
- Imágenes de portada
- Tipos de ediciones
- Enlaces directos de compra

Todo esto se recopila mediante **web scraping avanzado** desde múltiples fuentes
y se sirve a una página web que muestra los datos de manera limpia y amigable.

---

# Características Principales

### Backend de Web Scraping Multicore
- Scraping en paralelo utilizando **Task Parallelism** en C#
- Integración con:
  - **Steam Store**
  - **Eneba**
  - **Fanatical (via Puppeteer Sharp)**
  - *(Opcional: soporte experimental para CDKeys, G2A…)*
- Generación de archivo `resultados.json` con toda la data consolidada
- Extracción de:
  - Precios
  - Descuentos
  - Calificaciones
  - Horas de juego (SteamSpy + HLTB)
  - Imágenes (Steam CDN)
- Manejo de errores, timeouts y fallback
- Desacople modular para agregar nuevas tiendas fácilmente

### Frontend — GameDash (Página Web)
- Interfaz moderna desarrollada por el equipo frontend
- Cargado dinámico de `resultados.json`
- Tarjetas visuales por cada videojuego
- Filtros:
  - Por precio
  - Por tienda
  - Por calificación
- Buscador en tiempo real
- Diseño responsive
- Ideal para GitHub Pages

---

# Vista Pagina
<p align="center">
<img src="https://github.com/user-attachments/assets/c67ab944-224c-40cd-af4b-29fe9da11b32" width="350"/>
</p>


---

## Tecnologías Utilizadas

### Backend
- **C# (.NET)**  
- **HtmlAgilityPack**  
- **Puppeteer Sharp**  
- **Task Parallel Library**  
- **Newtonsoft JSON**  

### Frontend
- **HTML5 / CSS3 / JavaScript**  
- **Bootstrap / Tailwind (si aplica)**  
- **Fetch API**  
- **Diseño responsive**

---

## Instalación

### 1. Clonar el repositorio
 -Utilizar el comando "git clone "

### 2. Abrir el proyecto
-Hacer click izquierdo sobre "proyectoParalelismo.csproj" y abrir con VS

## Visualizar solo la pagina
https://cespgonzadev.github.io/gameDash/

## Autores
* **Andres Zumbado Vargas** - *Desarrollador* - [CespGonzaDev](https://github.com/CespGonzaDev)
* **Daniel Cespedes** - *Desarrollador* - [CespGonzaDev](https://github.com/CespGonzaDev)
* **Jean Carlos Segura** - *Desarrollador* - [Jeanksv1](https://github.com/Jeanksv1)






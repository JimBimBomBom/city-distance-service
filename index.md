---
layout: default
title: City Distance Search
---

<div style="display: flex; flex-direction: column; align-items: center; justify-content: center; height: 80vh; font-family: sans-serif;">

  <h1 style="margin-bottom: 1rem;">City Distance Finder</h1>

  <form id="distance-form" style="display: flex; flex-direction: column; align-items: center; gap: 0.75rem; width: 300px;">
    
    <input 
      type="text" 
      id="city1" 
      name="city1" 
      placeholder="Enter first city"
      style="width: 100%; padding: 0.5rem; border-radius: 0.5rem; border: 1px solid #ccc;"
      required
    />

    <input 
      type="text" 
      id="city2" 
      name="city2" 
      placeholder="Enter second city"
      style="width: 100%; padding: 0.5rem; border-radius: 0.5rem; border: 1px solid #ccc;"
      required
    />

    <button 
      type="submit"
      style="padding: 0.5rem 1.25rem; border: none; background-color: #007acc; color: white; border-radius: 0.5rem; cursor: pointer;"
    >
      Search
    </button>

  </form>

</div>

<script>
document.getElementById('distance-form').addEventListener('submit', function(event) {
  event.preventDefault();
  const city1 = document.getElementById('city1').value.trim();
  const city2 = document.getElementById('city2').value.trim();
  alert(`Searching for distance between ${city1} and ${city2}...`);
});
</script>

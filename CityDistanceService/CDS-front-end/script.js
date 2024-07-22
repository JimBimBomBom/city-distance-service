document.getElementById('distance-form').addEventListener('submit', async function(event) {
    event.preventDefault();

    const city1 = document.getElementById('city1').value;
    const city2 = document.getElementById('city2').value;

    try {
        const response = await fetch(`http://your-backend-api-url/distance?city1=${city1}&city2=${city2}`);
        if (!response.ok) {
            throw new Error('Network response was not ok ' + response.statusText);
        }
        const data = await response.json();
        document.getElementById('result').textContent = `The distance between ${city1} and ${city2} is ${data.distance} km.`;
    } catch (error) {
        document.getElementById('result').textContent = `Error: ${error.message}`;
    }
});

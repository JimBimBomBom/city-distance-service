document.getElementById('searchBtn').addEventListener('click', function() {
    const city1 = document.getElementById('city1').value;
    const city2 = document.getElementById('city2').value;

    console.log('Sending request to the server with:', { City1: city1, City2: city2 });

    fetch('https://citydistanceservice-app-v6cgvtuw4a-uc.a.run.app/distance', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Connection': 'keep-alive'
        },
        body: JSON.stringify({ City1: city1, City2: city2 })
    })
    .then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return response.json();
    })
    .then(data => {
        document.getElementById('result').innerText = `Distance between '${city1}' and '${city2}' is: ${data}`;
    })
    .catch(error => {
        console.error('Error:', error);
        document.getElementById('result').innerText = 'An error occurred. Please try again later.';
    });
});

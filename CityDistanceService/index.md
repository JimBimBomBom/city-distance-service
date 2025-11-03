<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <title>My App</title>
</head>
<body>
  <h1>Welcome to My App!</h1>
  <script>
    // Example of calling your backend
    fetch("https://your-backend-api.com/data")
      .then(r => r.json())
      .then(console.log);
  </script>
</body>
</html>

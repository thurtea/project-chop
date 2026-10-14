<?php
declare(strict_types=1);

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    header('Location: demo.html', true, 303);
    exit;
}

$name = trim((string) ($_POST['name'] ?? ''));
$email = trim((string) ($_POST['email'] ?? ''));
$note = trim((string) ($_POST['note'] ?? ''));
$trap = trim((string) ($_POST['company'] ?? ''));

if ($trap !== '') {
    header('Location: demo.html?sent=1', true, 303);
    exit;
}

$valid = $name !== ''
    && strlen($name) <= 80
    && filter_var($email, FILTER_VALIDATE_EMAIL)
    && strlen($email) <= 120
    && strlen($note) <= 1000;

if (!$valid) {
    header('Location: demo.html?error=1', true, 303);
    exit;
}

$name = str_replace(["\r", "\n"], ' ', $name);
$email = str_replace(["\r", "\n"], '', $email);
$note = str_replace("\r", '', $note);

$path = dirname(__DIR__, 2) . '/private_html/chopper-demo-requests.jsonl';
$row = json_encode([
    'at' => gmdate('c'),
    'name' => $name,
    'email' => $email,
    'note' => $note,
], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

if ($row === false || file_put_contents($path, $row . "\n", FILE_APPEND | LOCK_EX) === false) {
    header('Location: demo.html?error=1', true, 303);
    exit;
}

$body = "Name: {$name}\nEmail: {$email}\n\n{$note}\n";
$headers = "Reply-To: {$email}\r\n";
@mail('itsthurtea@gmail.com', 'Chopper demo request', $body, $headers);

header('Location: demo.html?sent=1', true, 303);
exit;

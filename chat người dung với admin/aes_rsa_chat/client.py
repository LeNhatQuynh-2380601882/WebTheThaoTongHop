from Crypto.Cipher import AES, PKCS1_OAEP
from Crypto.PublicKey import RSA
from Crypto.Util.Padding import pad, unpad
import socket
import threading
import sys
import json
import hashlib
import queue
import argparse
from flask import Flask, Response, request, jsonify, render_template

# Safe UTF-8 configuration for Windows standard streams
if sys.platform.startswith('win'):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

# Parse command line arguments
parser = argparse.ArgumentParser(description="AES-RSA Secure Chat Client")
parser.add_argument("--port", type=int, default=5001, help="Flask server port")
parser.add_argument("--username", type=str, default="Alice", help="Chat username")
args = parser.parse_args()

username = args.username
flask_port = args.port
socket_port = 12346

# Socket variables
client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
aes_key = None
encryption_info = {} # Information displayed in Security Panel

# Flask app configuration
app = Flask(__name__)
message_history = []
sse_queues = []
online_users = []
online_users_lock = threading.Lock()

def send_msg(sock, data):
    length = len(data)
    sock.sendall(length.to_bytes(4, 'big') + data)

def recv_msg(sock):
    try:
        raw_length = sock.recv(4)
        if not raw_length:
            return None
        length = int.from_bytes(raw_length, 'big')
        data = bytearray()
        while len(data) < length:
            packet = sock.recv(length - len(data))
            if not packet:
                return None
            data.extend(packet)
        return bytes(data)
    except Exception:
        return None

def encrypt_aes(key, plaintext_bytes):
    cipher = AES.new(key, AES.MODE_CBC)
    ciphertext = cipher.encrypt(pad(plaintext_bytes, AES.block_size))
    return cipher.iv + ciphertext

def decrypt_aes(key, ciphertext_with_iv):
    iv = ciphertext_with_iv[:16]
    ciphertext = ciphertext_with_iv[16:]
    cipher = AES.new(key, AES.MODE_CBC, iv)
    return unpad(cipher.decrypt(ciphertext), AES.block_size)

# Connect to Socket Server and perform RSA-AES handshaking
def connect_socket_server():
    global aes_key, encryption_info
    print(f"[Client] Connecting to Socket Server at localhost:{socket_port}...")
    try:
        client_socket.connect(('localhost', socket_port))
        print("[Client] Connected to server.")

        # Step 1: Generate Client RSA Key Pair
        print("[Client] Generating RSA keys (2048-bit)...")
        client_key = RSA.generate(2048)
        client_pub_pem = client_key.publickey().export_key(format='PEM').decode('utf-8')
        client_priv_pem = client_key.export_key(format='PEM').decode('utf-8')

        # Step 2: Receive Server RSA Public Key
        server_pub_bytes = client_socket.recv(2048)
        server_pub_pem = server_pub_bytes.decode('utf-8')
        server_public_key = RSA.import_key(server_pub_bytes)

        # Step 3: Send Client RSA Public Key
        send_msg(client_socket, client_pub_pem.encode('utf-8'))

        # Step 4: Receive encrypted AES key and decrypt it
        encrypted_aes_key = recv_msg(client_socket)
        cipher_rsa = PKCS1_OAEP.new(client_key)
        aes_key = cipher_rsa.decrypt(encrypted_aes_key)

        print("[Client] Key agreement completed.")
        print(f"Negotiated AES Key (Hex): {aes_key.hex()}")

        # Store for display in security panel
        encryption_info = {
            "algorithm": "AES-RSA Hybrid",
            "server_pub_key": server_pub_pem,
            "client_pub_key": client_pub_pem,
            "client_priv_key": client_priv_pem,
            "aes_key_hex": aes_key.hex()
        }

        # Step 5: Send encrypted username for registration
        reg_data = {"username": username}
        reg_encrypted = encrypt_aes(aes_key, json.dumps(reg_data).encode('utf-8'))
        send_msg(client_socket, reg_encrypted)

        # Launch background socket receiver thread
        t = threading.Thread(target=socket_receiver)
        t.daemon = True
        t.start()

    except ConnectionRefusedError:
        print("[Client] Error: Socket server is not running or connection refused.")
        sys.exit(1)
    except Exception as e:
        print(f"[Client] Error during handshaking: {e}")
        sys.exit(1)

def socket_receiver():
    global online_users
    while True:
        try:
            encrypted_payload = recv_msg(client_socket)
            if not encrypted_payload:
                print("[Client] Disconnected from Socket Server.")
                notify_web({"type": "system", "content": "Mất kết nối với Socket Server."})
                break

            # Decrypt the payload
            decrypted_bytes = decrypt_aes(aes_key, encrypted_payload)
            msg_data = json.loads(decrypted_bytes.decode('utf-8'))

            if msg_data.get("type") == "user_list":
                with online_users_lock:
                    online_users = msg_data.get("users", [])
                notify_web(msg_data)

            elif msg_data.get("type") == "system":
                notify_web(msg_data)

            elif msg_data.get("type") == "message":
                sender = msg_data.get("sender")
                content = msg_data.get("content")
                client_hash = msg_data.get("hash")
                raw_hex = msg_data.get("raw_hex", encrypted_payload.hex())

                # Verify SHA-256 integrity
                computed_hash = hashlib.sha256(content.encode('utf-8')).hexdigest()
                is_valid = (computed_hash == client_hash)

                formatted_msg = {
                    "type": "message",
                    "sender": sender,
                    "content": content,
                    "hash": client_hash,
                    "hash_type": "SHA-256",
                    "integrity_valid": is_valid,
                    "raw_hex": raw_hex
                }
                
                # Append to history and push to connected browsers
                message_history.append(formatted_msg)
                notify_web(formatted_msg)

        except Exception as e:
            print(f"[Client] Error in socket receiver: {e}")
            break

def notify_web(data):
    # Push to all active browser SSE streams
    for q in list(sse_queues):
        try:
            q.put_nowait(data)
        except Exception:
            pass

# Flask Route - Web Interface
@app.route('/')
def index():
    return render_template('index.html', username=username, encryption_info=encryption_info)

# Flask Route - Server-Sent Events for live updates
@app.route('/events')
def events():
    def event_stream():
        q = queue.Queue()
        sse_queues.append(q)
        try:
            # Yield initial connection confirmation
            yield f"data: {json.dumps({'type': 'connection_status', 'status': 'connected', 'username': username, 'encryption_info': encryption_info, 'online_users': online_users})}\n\n"
            
            # Send message history
            for msg in message_history:
                yield f"data: {json.dumps(msg)}\n\n"

            while True:
                msg = q.get() # blocks
                yield f"data: {json.dumps(msg)}\n\n"
        except GeneratorExit:
            sse_queues.remove(q)
    return Response(event_stream(), mimetype="text/event-stream")

# Flask Route - Send Message from Browser
@app.route('/send', methods=['POST'])
def send_message_api():
    content = request.json.get('content')
    if not content:
        return jsonify({"status": "error", "message": "Empty message"}), 400

    try:
        # Step 1: Calculate SHA-256 hash for message integrity
        msg_hash = hashlib.sha256(content.encode('utf-8')).hexdigest()

        # Step 2: Construct JSON structure
        payload_data = {
            "type": "message",
            "content": content,
            "hash": msg_hash
        }

        # Step 3: Encrypt the payload with the AES key
        encrypted_payload = encrypt_aes(aes_key, json.dumps(payload_data).encode('utf-8'))

        # Step 4: Send the encrypted bytes over TCP socket
        send_msg(client_socket, encrypted_payload)

        # For visual consistency, immediately append to message history of this client too
        # But server also broadcasts back, so we let receiver handle it to avoid duplicate messages.
        return jsonify({"status": "ok"})
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

if __name__ == "__main__":
    # Perform handshaking with socket server before starting Web service
    connect_socket_server()

    # Disable flask output logging to keep terminal clean
    import logging
    log = logging.getLogger('werkzeug')
    log.setLevel(logging.ERROR)

    print(f"\n[Client] Web interface is running at: http://localhost:{flask_port}")
    print(f"[Client] Opening chat room as {username}...")
    
    # Run Flask local web service
    app.run(host='localhost', port=flask_port, debug=False, threaded=True)

import socket
import json
import time
import argparse
import os

# 管理サーバーの接続情報
HOST = os.environ.get('OPENGS_MANAGEMENT_HOST', '127.0.0.1')
PORT = int(os.environ.get('OPENGS_MANAGEMENT_PORT', '50020'))

def send_json(sock, data):
    message = json.dumps(data) + '\n'
    sock.sendall(message.encode('utf-8'))
    print(f"送信: {message.strip()}")

def receive_json(sock):
    buffer = b''
    while True:
        data = sock.recv(1024)
        if not data:
            return None
        buffer += data
        if b'\n' in buffer:
            message = buffer.split(b'\n', 1)[0]
            buffer = buffer[len(message) + 1:]
            try:
                return json.loads(message.decode('utf-8'))
            except json.JSONDecodeError:
                continue

def main():
    parser = argparse.ArgumentParser(description='OpenGS management server smoke client')
    parser.add_argument('--host', default=HOST)
    parser.add_argument('--port', type=int, default=PORT)
    parser.add_argument('--admin-id', default=os.environ.get('OPENGS_ADMIN_ID', 'admin'))
    parser.add_argument('--admin-password', default=os.environ.get('OPENGS_ADMIN_PASSWORD', 'admin123'))
    args = parser.parse_args()

    try:
        # ソケット接続
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(10)
        sock.connect((args.host, args.port))
        print(f"管理サーバーに接続しました: {args.host}:{args.port}")
        
        # 接続成功メッセージの受信
        response = receive_json(sock)
        if response:
            print(f"受信: {response}")
        
        # 管理者ログイン
        login_data = {
            "MessageType": "AdminLoginRequest",
            "AdminID": args.admin_id,
            "AdminPassword": args.admin_password
        }
        send_json(sock, login_data)
        response = receive_json(sock)
        if response:
            print(f"受信: {response}")
        
        # サーバーステータス取得
        status_data = {
            "MessageType": "ServerStatusRequest"
        }
        send_json(sock, status_data)
        response = receive_json(sock)
        if response:
            print(f"受信: {response}")
        
        # 接続中のユーザー一覧取得
        users_data = {
            "MessageType": "GetConnectedUsersRequest"
        }
        send_json(sock, users_data)
        response = receive_json(sock)
        if response:
            print(f"受信: {response}")
        
        # ブロードキャストメッセージ送信
        broadcast_data = {
            "MessageType": "BroadcastMessageRequest",
            "Message": "これはテストブロードキャストメッセージです。"
        }
        send_json(sock, broadcast_data)
        response = receive_json(sock)
        if response:
            print(f"受信: {response}")
        
        # 管理者ログアウト
        logout_data = {
            "MessageType": "AdminLogoutRequest"
        }
        send_json(sock, logout_data)
        response = receive_json(sock)
        if response:
            print(f"受信: {response}")
        
        # 接続の切断
        sock.sendall(b'!\n')
        sock.close()
        print("管理サーバーとの接続を切断しました。")
        
    except Exception as e:
        print(f"エラー: {e}")
        if 'sock' in locals():
            sock.close()

if __name__ == "__main__":
    main()

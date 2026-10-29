using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace OpenGSServer;

/// <summary>
/// �Ǘ��҃A�J�E���g�̃V���v���ȉi�����}�l�[�W���B
/// - ���K�͂Ȏ�v���W�F�N�g�����̍ŏ����̋@�\�̂ݒ�
/// - JSON�t�@�C���ɑS�A�J�E���g��ۑ�/�ǂݍ���
/// - �X���b�h�Z�[�t�i�������b�N�j
/// </summary>
public sealed class AdminManager
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ServerAdminAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; private set; }

    public AdminManager(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>
    /// ���ݓo�^����Ă���Ǘ���ID�ꗗ�i�R�s�[�j
    /// </summary>
    public List<string> ListAdminIds()
    {
        lock (_lock)
        {
            return _accounts.Keys.ToList();
        }
    }

    /// <summary>
    /// �w��t�@�C������ǂݍ��݁i���݂��Ȃ��ꍇ�͋�ŕԂ��j
    /// </summary>
    public void Load()
    {
        lock (_lock)
        {
            _accounts.Clear();

            if (!File.Exists(FilePath)) return;

            var json = File.ReadAllText(FilePath);
            try
            {
                // The server publishes with PublishAot, so reflection based
                // serialization is unavailable. AdminJsonSerializerContext is
                // the generated resolver for these types.
                var list = JsonSerializer.Deserialize(json, AdminJsonSerializerContext.Default.ListServerAdminAccount);
                if (list == null) return;

                foreach (var a in list)
                {
                    if (string.IsNullOrWhiteSpace(a.Id)) continue;
                    _accounts[a.Id] = a;
                }
            }
            catch (Exception)
            {
                // �ǂݍ��ݎ��s�͖������ċ��Ԃɂ���i���O�͌Ăяo�����Ŗ]�ނȂ�ǉ��\�j
            }
        }
    }

    /// <summary>
    /// ���݂̃A�J�E���g�ꗗ���t�@�C���ɕۑ��i���q�I�ɏ������ށj
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            var list = _accounts.Values.ToList();
            var json = JsonSerializer.Serialize(list, AdminJsonSerializerContext.Default.ListServerAdminAccount);

            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            // �㏑���͌��q�I��
            File.Copy(tmp, FilePath, overwrite: true);
            File.Delete(tmp);
        }
    }

    /// <summary>
    /// �Ǘ��҂�ǉ��B���ɑ��݂���ID�������false��Ԃ��B
    /// </summary>
    public bool AddAdmin(string id, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id required", nameof(id));
        if (string.IsNullOrWhiteSpace(plainPassword)) throw new ArgumentException("password required", nameof(plainPassword));

        lock (_lock)
        {
            if (_accounts.ContainsKey(id)) return false;
            var account = ServerAdminAccount.Create(id, plainPassword);
            _accounts[id] = account;
            return true;
        }
    }

    /// <summary>
    /// �Ǘ��҂̍폜�B���݂����true�B
    /// </summary>
    public bool RemoveAdmin(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_lock)
        {
            return _accounts.Remove(id);
        }
    }

    /// <summary>
    /// �w��ID�̃p�X���[�h������
    /// </summary>
    public bool VerifyAdmin(string id, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(plainPassword)) return false;
        lock (_lock)
        {
            if (!_accounts.TryGetValue(id, out var account)) return false;
            return account.VerifyPassword(plainPassword);
        }
    }

    /// <summary>
    /// �p�X���[�h�ύX�B���������true�B
    /// </summary>
    public bool ChangePassword(string id, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newPassword)) return false;
        lock (_lock)
        {
            if (!_accounts.TryGetValue(id, out var account)) return false;

            // Create a new account instance (ServerAdminAccount is immutable-ish)
            if (!account.VerifyPassword(currentPassword)) return false;

            var updated = ServerAdminAccount.Create(id, newPassword);
            _accounts[id] = updated;
            return true;
        }
    }

    /// <summary>
    /// �Ǘ��p�t�@�C���̊���p�X���g�p���郆�[�e�B���e�B
    /// </summary>
    public static AdminManager CreateDefault()
    {
        var path = Path.Combine(UnityPaths.PersistentDataPath, "admin_accounts.json");
        return new AdminManager(path);
    }
}

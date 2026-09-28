// w_PetProcess.cpp: implementation of the PetProcess class.
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "Data/DataHandler/PetDataLayout.h"
#include <array>
#include <cmath>
#include <memory>
#include "w_PetActionStand.h"
#include "w_PetActionRound.h"
#include "w_PetActionDemon.h"
#include "w_PetActionCollecter.h"
#include "w_PetActionCollecter_Add.h"
#include "w_PetActionUnicorn.h"
#include "w_PetProcess.h"
#include "Core/Utilities/ReadScript.h"


PetInfoPtr PetInfo::Make()
{
    PetInfoPtr petInfo(new PetInfo);
    return petInfo;
}

PetInfo::PetInfo() :
    m_scale(0.0f),
    m_actions(NULL),
    m_speeds(NULL),
    m_count(0)
{
}

PetInfo::~PetInfo()
{
    Destroy();
}

void PetInfo::Destroy()
{

    delete[] m_actions;
    m_actions = NULL;
    delete[] m_speeds;
    m_speeds = NULL;
}

void PetInfo::SetActions(int count, int* actions, float* speeds)
{
    constexpr int MAX_PET_ACTIONS = 100;

    if (NULL == actions || NULL == speeds || count <= 0 || count > MAX_PET_ACTIONS)
    {
        return;
    }

    Destroy();

    m_count = count;
    m_actions = new int[count]();
    memcpy(m_actions, actions, sizeof(int) * m_count);

    m_speeds = new float[count];
    memcpy(m_speeds, speeds, sizeof(float) * m_count);
}

PetProcessPtr g_petProcess;

PetProcess& ThePetProcess()
{
    assert(g_petProcess);
    return *g_petProcess;
}

PetProcessPtr PetProcess::Make()
{
    PetProcessPtr petprocess(new PetProcess);
    petprocess->Init();
    return petprocess;
}

PetProcess::PetProcess()
{
}

PetProcess::~PetProcess()
{
    Destroy();
}

void PetProcess::Init()
{
    PetActionStandPtr actionStand = PetActionStand::Make();
    m_petsAction.insert(make_pair(PC4_ELF, actionStand));
    //m_petsAction.insert( make_pair( 4, actionStand ) );

    PetActionRoundPtr actionRound = PetActionRound::Make();
    m_petsAction.insert(make_pair(PC4_TEST, actionRound));

    PetActionDemonPtr actionTest = PetActionDemon::Make();
    m_petsAction.insert(make_pair(PC4_SATAN, actionTest));

    PetActionCollecterPtr actionCollecter = PetActionCollecter::Make();
    m_petsAction.insert(make_pair(XMAS_RUDOLPH, actionCollecter));

#ifdef PJH_ADD_PANDA_PET
    PetActionCollecterAddPtr actionCollecter_Add = PetActionCollecterAdd::Make();
    m_petsAction.insert(make_pair(PANDA, actionCollecter_Add));
#endif //#ifdef PJH_ADD_PANDA_PET

    PetActionUnicornPtr actionUnicorn = PetActionUnicorn::Make();
    m_petsAction.insert(make_pair(UNICORN, actionUnicorn));

    PetActionCollecterSkeletonPtr actionCollecter_Skeleton = PetActionCollecterSkeleton::Make();
    m_petsAction.insert(make_pair(SKELETON, actionCollecter_Skeleton));

    LoadData();
}

void PetProcess::Destroy()
{
    for (auto iter = m_petsList.begin(); iter != m_petsList.end(); )
    {
        auto tempiter = iter;
        ++iter;
        Weak_Ptr(PetObject) basepet = *tempiter;

        if (basepet.expired() == FALSE)
        {
            basepet.lock()->Release();
            m_petsList.erase(tempiter);
        }
    }
    m_petsList.clear();

    m_petsAction.clear();
}

Weak_Ptr(PetAction) PetProcess::Find(int key)
{
    auto iter = m_petsAction.find(key);

    if (iter != m_petsAction.end())
    {
        return (*iter).second;
    }

    Weak_Ptr(PetAction) temp;

    return temp;
}

void PetProcess::Register(Smart_Ptr(PetObject) pPet)
{
    m_petsList.push_back(pPet);
}

void PetProcess::UnRegister(CHARACTER* Owner, int itemType, bool isUnregistAll)
{
    if (NULL == Owner) return;

    for (auto iter = m_petsList.begin(); iter != m_petsList.end(); )
    {
        auto tempiter = iter;
        ++iter;
        Weak_Ptr(PetObject) basepet = *tempiter;

        if (basepet.expired() == FALSE)
        {
            if ((-1 == itemType && basepet.lock()->IsSameOwner(&Owner->Object))
                || basepet.lock()->IsSameObject(&Owner->Object, itemType))
            {
                basepet.lock()->Release();
                m_petsList.erase(tempiter);

                if (-1 == itemType || !isUnregistAll) return;
            }
        }
    }
}

namespace
{
    std::optional<Data::Pets::PetDataLayout> ReadPetData(FILE* file, std::vector<BYTE>& payload)
    {
        if (fseek(file, 0, SEEK_END) != 0)
            return std::nullopt;
        const auto fileSize = ftell(file);
        if (fileSize < 0 || fseek(file, 0, SEEK_SET) != 0)
            return std::nullopt;
        std::array<std::int32_t, 3> header {};
        if (fread(header.data(), sizeof(header), 1, file) != 1)
            return std::nullopt;
        const auto layout = Data::Pets::PetDataLayout::Validate(header[1], header[2], static_cast<std::size_t>(fileSize));
        if (!layout)
            return std::nullopt;
        payload.resize(layout->payloadSize);
        DWORD checksum = 0;
        if (fread(payload.data(), 1, payload.size(), file) != payload.size()
            || fread(&checksum, sizeof(checksum), 1, file) != 1
            || checksum != GenerateCheckSum2(payload.data(), layout->payloadSize, 0x7F1D))
            return std::nullopt;
        return layout;
    }

    PetInfoPtr ReadPetRecord(BYTE* record, const Data::Pets::PetDataLayout& layout,
        int& type, std::vector<int>& actions, std::vector<float>& speeds)
    {
        BuxConvert(record, layout.recordSize);
        int blendMesh = -1;
        float scale = 0;
        int count = 0;
        constexpr int BlendMeshOffset = sizeof(std::int32_t);
        constexpr int ScaleOffset = 2 * sizeof(std::int32_t);
        constexpr int CountOffset = 3 * sizeof(std::int32_t);
        memcpy(&type, record, sizeof(type));
        memcpy(&blendMesh, record + BlendMeshOffset, sizeof(blendMesh));
        memcpy(&scale, record + ScaleOffset, sizeof(scale));
        memcpy(&count, record + CountOffset, sizeof(count));
        constexpr int MaximumPetActions = 100;
        constexpr int MaximumPetType = 10000;
        if (type < 0 || type > MaximumPetType || count <= 0 || count > layout.actionSlots
            || count > MaximumPetActions || !std::isfinite(scale))
            return {};

        constexpr int RecordHeaderSize = 4 * sizeof(std::int32_t);
        memcpy(actions.data(), record + RecordHeaderSize, sizeof(int) * layout.actionSlots);
        memcpy(speeds.data(), record + RecordHeaderSize + sizeof(int) * layout.actionSlots, sizeof(float) * layout.actionSlots);
        if (!std::all_of(speeds.begin(), speeds.begin() + count, [](float speed) { return std::isfinite(speed); }))
            return {};
        auto info = PetInfo::Make();
        info->SetBlendMesh(blendMesh);
        info->SetScale(scale);
        info->SetActions(count, actions.data(), speeds.data());
        return info;
    }
}

bool PetProcess::LoadData()
{
    constexpr auto FileName = L"Data\\Local\\pet.bmd";
    using FileHandle = std::unique_ptr<FILE, decltype(&fclose)>;
    FileHandle file(_wfopen(FileName, L"rb"), &fclose);
    std::vector<BYTE> payload;
    const auto layout = file ? ReadPetData(file.get(), payload) : std::nullopt;
    if (!layout)
    {
        constexpr auto Message = L"Data\\Local\\pet.bmd is missing, truncated, or corrupt.";
        g_ErrorReport.Write(Message);
        MessageBox(g_hWnd, Message, nullptr, MB_OK);
        SendMessage(g_hWnd, WM_DESTROY, 0, 0);
        return false;
    }

    std::vector<int> actions(layout->actionSlots);
    std::vector<float> speeds(layout->actionSlots);
    for (int index = 0; index < layout->recordCount; ++index)
    {
        int type = 0;
        auto info = ReadPetRecord(payload.data() + index * layout->recordSize, *layout, type, actions, speeds);
        if (info)
            m_petsInfo.insert(make_pair(ITEM_HELPER + type, info));
    }
    return true;
}

bool PetProcess::IsPet(int itemType)
{
    auto iter = m_petsInfo.find(itemType);
    if (iter == m_petsInfo.end()) return FALSE;

    Weak_Ptr(PetInfo) petInfo = (*iter).second;
    if (petInfo.expired()) return FALSE;

    return TRUE;
}

bool PetProcess::CreatePet(int itemType, int modelType, vec3_t Position, CHARACTER* Owner, int SubType, int LinkBone)
{
    if (NULL == Owner) return FALSE;

    PetObjectPtr _tempPet = PetObject::Make();
    if (_tempPet->Create(itemType, modelType, Position, Owner, SubType, LinkBone))
    {
        auto iter = m_petsInfo.find(itemType);
        if (iter == m_petsInfo.end()) return FALSE;

        Weak_Ptr(PetInfo) petInfo = (*iter).second;

        int _count = 0;
        int* action = NULL;
        float* speed = NULL;
        if (petInfo.expired() == FALSE)
        {
            _count = petInfo.lock()->GetActionsCount();
            action = petInfo.lock()->GetActions();
            speed = petInfo.lock()->GetSpeeds();

            _tempPet->SetScale(petInfo.lock()->GetScale());
            _tempPet->SetBlendMesh(petInfo.lock()->GetBlendMesh());
        }

        for (int i = 0; i < _count; i++)
        {
            _tempPet->SetActions((PetObject::ActionType)i, Find(action[i]), speed[i]);
        }
        Register(_tempPet);
        return TRUE;
    }

    return FALSE;
}

void PetProcess::DeletePet(CHARACTER* Owner, int itemType, bool allDelete)
{
    if (NULL == Owner) return;

    UnRegister(Owner, itemType, allDelete);
}

void PetProcess::SetCommandPet(CHARACTER* Owner, int targetKey, PetObject::ActionType cmdType)
{
    if (NULL == Owner) return;

    for (auto iter = m_petsList.begin(); iter != m_petsList.end(); )
    {
        auto tempiter = iter;
        ++iter;
        Weak_Ptr(PetObject) basepet = *tempiter;

        if (basepet.expired() == FALSE)
        {
            if (basepet.lock()->IsSameOwner(&Owner->Object))
            {
                basepet.lock()->SetCommand(targetKey, cmdType);
            }
        }
    }
}

void PetProcess::UpdatePets()
{
    for (auto iter = m_petsList.begin(); iter != m_petsList.end(); )
    {
        auto tempiter = iter;
        ++iter;
        Weak_Ptr(PetObject) basepet = *tempiter;

        if (basepet.expired() == false)
        {
            basepet.lock()->Update();
        }
    }
}

void PetProcess::RenderPets()
{
    for (auto iter = m_petsList.begin(); iter != m_petsList.end(); )
    {
        auto tempiter = iter;
        ++iter;
        Weak_Ptr(PetObject) basepet = *tempiter;

        if (basepet.expired() == FALSE)
        {
            basepet.lock()->Render();
        }
    }
}
